using System.Globalization;
using System.Text.Json;

namespace KrxScreen;

public record Quote(string Code, string Name, string Market, double Price, double Change, double ChangeRate,
                    double TradeValue, double Cap);

// 종목별 연도 시계열 (스크리닝 때 모아 둔 것)
public record StockSeries(Dictionary<int, double> NetIncome, Dictionary<int, double> Dps);

// 공공데이터포털 '금융위원회_주식시세정보' 전 종목 시세 + 로컬 캐시
public static class Market
{
    static readonly string[] GoUrls =
    {
        "https://apis.data.go.kr/1160100/GetStockSecuritiesInfoService_V2/getStockPriceInfo_V2",
        "https://apis.data.go.kr/1160100/service/GetStockSecuritiesInfoService/getStockPriceInfo",
    };

    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KrxScreen");
    static string QuoteFile => Path.Combine(Dir, "quotes.json");
    public static string ScreenFile => Path.Combine(Dir, "screen.xml");
    public static string SeriesFile => Path.Combine(Dir, "series.json");

    record QuoteCache(string BasDt, List<Quote> Quotes);

    static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p)) return 0;
        var s = (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()).Replace(",", "").Trim();
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()) : null;

    // 가장 최근 거래일의 코스피·코스닥 전 종목 (우선주 포함). 데이터는 기준일 다음 영업일 13시 이후 갱신.
    public static async Task<(string basDt, List<Quote> quotes)> Fetch(string goKey, HttpClient http)
    {
        string lastErr = "";
        foreach (var url in GoUrls)
        {
            var day = DateTime.Today;
            for (int i = 0; i < 10; i++, day = day.AddDays(-1))
            {
                string d = day.ToString("yyyyMMdd");
                string q = $"{url}?serviceKey={Uri.EscapeDataString(goKey.Trim())}&resultType=json&numOfRows=5000&pageNo=1&basDt={d}";
                string txt = "";
                var raw = new List<JsonElement>();
                try
                {
                    using var resp = await http.GetAsync(q);
                    txt = await resp.Content.ReadAsStringAsync();
                    resp.EnsureSuccessStatusCode();
                    using var doc = JsonDocument.Parse(txt);
                    var items = doc.RootElement.GetProperty("response").GetProperty("body").GetProperty("items");
                    if (items.ValueKind == JsonValueKind.Object && items.TryGetProperty("item", out var it))
                    {
                        if (it.ValueKind == JsonValueKind.Array) raw.AddRange(it.EnumerateArray().Select(x => x.Clone()));
                        else if (it.ValueKind == JsonValueKind.Object) raw.Add(it.Clone());
                    }
                }
                catch (Exception ex)
                {
                    lastErr += $"\r\n{url}\r\n{ex.Message}\r\n응답 앞부분: {(txt.Length > 300 ? txt[..300] : txt)}\r\n";
                    break; // 이 주소는 안 됨 -> 다음 주소
                }
                if (raw.Count == 0) continue;

                var list = new List<Quote>();
                foreach (var r in raw)
                {
                    string code = Str(r, "srtnCd") ?? "";
                    if (code.Length > 6) code = code[^6..];
                    string mkt = Str(r, "mrktCtg");
                    if (mkt != "KOSPI" && mkt != "KOSDAQ") continue; // 코넥스 제외
                    list.Add(new Quote(code, (Str(r, "itmsNm") ?? "").Trim(), mkt, Num(r, "clpr"), Num(r, "vs"),
                        Num(r, "fltRt"), Num(r, "trPrc"), Num(r, "mrktTotAmt")));
                }
                Save(new QuoteCache(d, list));
                return (d, list);
            }
        }
        throw new Exception("시세를 못 받았어요. 키 활성화(승인 후 최대 1시간)를 확인해주세요.\r\n" + lastErr);
    }

    public static (string basDt, List<Quote> quotes) LoadCached()
    {
        try
        {
            if (!File.Exists(QuoteFile)) return (null, null);
            var c = JsonSerializer.Deserialize<QuoteCache>(File.ReadAllText(QuoteFile));
            return (c.BasDt, c.Quotes);
        }
        catch { return (null, null); } // 캐시가 깨졌으면 새로 받으면 됨
    }

    static void Save(QuoteCache c)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(QuoteFile, JsonSerializer.Serialize(c));
    }

    // 가치 분석 결과 저장 (다음 실행·시장 검색·배당 계산에 씀)
    public static void SaveScreen(System.Data.DataTable t, Dictionary<string, StockSeries> s)
    {
        Directory.CreateDirectory(Dir);
        t.TableName = "screen";
        t.WriteXml(ScreenFile, System.Data.XmlWriteMode.WriteSchema);
        File.WriteAllText(SeriesFile, JsonSerializer.Serialize(s));
    }

    public static Dictionary<string, StockSeries> LoadSeries()
    {
        try { return File.Exists(SeriesFile) ? JsonSerializer.Deserialize<Dictionary<string, StockSeries>>(File.ReadAllText(SeriesFile)) : new(); }
        catch { return new(); }
    }
}
