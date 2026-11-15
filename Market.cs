using System.Globalization;
using System.Text.Json;

namespace KrxScreen;

// Price·Change는 그 나라 통화, TradeValue·Cap은 원화(미국은 환율로 환산)
public record Quote(string Code, string Name, string Market, double Price, double Change, double ChangeRate,
                    double TradeValue, double Cap)
{
    public string Currency { get; init; } = "KRW";
    public double? DivYield { get; init; } // %, 미국만 (국내는 DART 결산에서 계산)
    public double? Dps { get; init; }      // 연간 주당배당금, 그 나라 통화
    public bool IsUs => Currency == "USD";
    public string NameEn { get; init; } // 미국 종목의 원래 영어 이름 (Name은 한글 별명)

    // Yahoo에서 덧붙이는 업종·추세 (모두 %). 국장은 KrTrends로 채운다.
    public string Sector { get; init; }
    public double? Chg1y { get; init; }
    public double? Vs50 { get; init; }
    public double? Vs200 { get; init; }
    public double? FromHigh { get; init; }
}

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

    // ----- 미국 (Yahoo) -----
    public static double UsdKrw = 1300; // 받기 전 임시값, 캐시나 Yahoo에서 덮어쓴다
    static string UsFile => Path.Combine(Dir, "us_quotes.json");
    record UsCache(DateTime At, double Fx, List<Quote> Quotes);

    public static async Task<List<Quote>> FetchUs()
    {
        var (q, fx) = await Yahoo.Fetch();
        UsdKrw = fx;
        Directory.CreateDirectory(Dir);
        File.WriteAllText(UsFile, JsonSerializer.Serialize(new UsCache(DateTime.Now, fx, q)));
        return q;
    }

    public static List<Quote> LoadUsCached()
    {
        try
        {
            if (!File.Exists(UsFile)) return null;
            var c = JsonSerializer.Deserialize<UsCache>(File.ReadAllText(UsFile));
            UsdKrw = c.Fx;
            return c.Quotes;
        }
        catch { return null; }
    }

    public static double Fx(string currency) => currency == "USD" ? UsdKrw : 1;

    // ----- 국장 업종·추세 (Yahoo) -----
    static string KrTrendFile => Path.Combine(Dir, "kr_trends.json");

    public static async Task<Dictionary<string, Yahoo.Trend>> FetchKrTrends()
    {
        var t = await Yahoo.KrTrends();
        Directory.CreateDirectory(Dir);
        File.WriteAllText(KrTrendFile, JsonSerializer.Serialize(t));
        return t;
    }

    public static Dictionary<string, Yahoo.Trend> LoadKrTrends()
    {
        try { return File.Exists(KrTrendFile) ? JsonSerializer.Deserialize<Dictionary<string, Yahoo.Trend>>(File.ReadAllText(KrTrendFile)) : null; }
        catch { return null; }
    }

    public static List<Quote> WithTrends(List<Quote> kr, Dictionary<string, Yahoo.Trend> t) =>
        kr == null || t == null ? kr : kr.Select(q => t.TryGetValue(q.Code, out var x)
            ? q with { Sector = x.Sector, Chg1y = x.Chg1y, Vs50 = x.Vs50, Vs200 = x.Vs200, FromHigh = x.FromHigh }
            : q).ToList();

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
