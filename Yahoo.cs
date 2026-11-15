using System.Net;
using System.Text;
using System.Text.Json;

namespace KrxScreen;

// Yahoo Finance 비공식 API (개인용). 쿠키 + crumb를 받아 screener로 미국 주식·ETF를 한 번에 가져온다.
// 장중 시세는 약 15분 지연.
public static class Yahoo
{
    const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
    const string Host = "https://query2.finance.yahoo.com";
    static readonly HttpClient http = NewHttp();
    static string crumb;

    static HttpClient NewHttp()
    {
        var h = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        h.DefaultRequestHeaders.UserAgent.ParseAdd(UA);
        return h;
    }

    static async Task EnsureCrumb()
    {
        if (crumb != null) return;
        using (await http.GetAsync("https://fc.yahoo.com")) { } // 404여도 쿠키는 받는다
        var c = (await http.GetStringAsync(Host + "/v1/test/getcrumb")).Trim();
        if (c == "" || c.Contains('<') || c.Contains(' ')) throw new Exception("Yahoo 인증(crumb)을 못 받았어요.");
        crumb = c;
    }

    static double D(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

    static string S(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    // 원/달러 환율
    static async Task<double> UsdKrw()
    {
        var txt = await http.GetStringAsync($"{Host}/v7/finance/quote?symbols=KRW%3DX&crumb={Uri.EscapeDataString(crumb)}");
        using var doc = JsonDocument.Parse(txt);
        var r = doc.RootElement.GetProperty("quoteResponse").GetProperty("result");
        return r.GetArrayLength() > 0 ? D(r[0], "regularMarketPrice") : 0;
    }

    // quoteType = EQUITY(시총 기준) 또는 ETF(순자산 기준), 기준값(달러) 이상을 큰 순서로 전부
    static async Task Screen(string quoteType, string field, double min, double fx, List<Quote> list)
    {
        for (int offset = 0; offset < 10_000; offset += 250)
        {
            var body = JsonSerializer.Serialize(new
            {
                size = 250, offset, sortField = field, sortType = "DESC", quoteType,
                query = new
                {
                    @operator = "AND",
                    operands = new object[]
                    {
                        new { @operator = "eq", operands = new object[] { "region", "us" } },
                        new { @operator = "gt", operands = new object[] { field, min } },
                    },
                },
            });
            using var resp = await http.PostAsync($"{Host}/v1/finance/screener?crumb={Uri.EscapeDataString(crumb)}&lang=en-US&region=US",
                new StringContent(body, Encoding.UTF8, "application/json"));
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                crumb = null; // 다음 갱신 때 다시 받는다
                throw new Exception($"Yahoo 인증 만료 ({(int)resp.StatusCode})");
            }
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var result = doc.RootElement.GetProperty("finance").GetProperty("result")[0];
            var quotes = result.GetProperty("quotes");
            foreach (var q in quotes.EnumerateArray())
            {
                string exch = S(q, "fullExchangeName") ?? "";
                if (exch.Contains("OTC")) continue; // 장외 제외
                double price = D(q, "regularMarketPrice");
                double yield = D(q, "dividendYield"); // % 단위
                double size = quoteType == "ETF" ? D(q, "netAssets") : D(q, "marketCap");
                list.Add(new Quote(S(q, "symbol"), S(q, "shortName") ?? S(q, "longName") ?? S(q, "symbol"),
                    quoteType == "ETF" ? "US ETF" : "US", price, D(q, "regularMarketChange"), D(q, "regularMarketChangePercent"),
                    D(q, "regularMarketVolume") * price * fx, size * fx)
                {
                    Currency = "USD",
                    DivYield = yield > 0 ? yield : null,
                    Dps = yield > 0 ? price * yield / 100 : null,
                });
            }
            int total = result.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
            if (quotes.GetArrayLength() < 250 || offset + 250 >= total) break;
        }
    }

    // 여러 심볼의 현재가·배당률(%). 국내 상장 ETF(.KS)도 된다 (단, 국내 ETF는 배당률이 비어 있다)
    public static async Task<Dictionary<string, (double price, double? yield)>> Quotes(IEnumerable<string> symbols)
    {
        await EnsureCrumb();
        var txt = await http.GetStringAsync(
            $"{Host}/v7/finance/quote?symbols={Uri.EscapeDataString(string.Join(",", symbols))}&crumb={Uri.EscapeDataString(crumb)}");
        using var doc = JsonDocument.Parse(txt);
        var map = new Dictionary<string, (double, double?)>();
        foreach (var q in doc.RootElement.GetProperty("quoteResponse").GetProperty("result").EnumerateArray())
        {
            double y = D(q, "dividendYield");
            map[S(q, "symbol")] = (D(q, "regularMarketPrice"), y > 0 ? y : null);
        }
        return map;
    }

    // 국장 종목코드 → Yahoo 심볼 (코스피 .KS, 코스닥 .KQ). 미국은 그대로.
    public static string Symbol(Quote q) => q.IsUs ? q.Code : q.Code + (q.Market == "KOSPI" ? ".KS" : ".KQ");

    public record Point(DateTime Date, double Close);

    // range: 1mo / 1y / 5y. 5년은 주 단위, 나머지는 일 단위. crumb 없이 된다.
    public static async Task<List<Point>> Chart(string symbol, string range)
    {
        string interval = range == "5y" ? "1wk" : "1d";
        var txt = await http.GetStringAsync($"{Host}/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range={range}&interval={interval}");
        using var doc = JsonDocument.Parse(txt);
        var result = doc.RootElement.GetProperty("chart").GetProperty("result");
        var list = new List<Point>();
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return list;
        var r = result[0];
        if (!r.TryGetProperty("timestamp", out var ts)) return list;
        var close = r.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");
        for (int i = 0; i < ts.GetArrayLength(); i++)
            if (close[i].ValueKind == JsonValueKind.Number) // 휴장일 등은 null
                list.Add(new Point(DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).LocalDateTime.Date, close[i].GetDouble()));
        return list;
    }

    public record Profile(string Sector, string Industry, long Employees, string Website, string Summary, string Category);

    public static async Task<Profile> GetProfile(string symbol)
    {
        await EnsureCrumb();
        var txt = await http.GetStringAsync(
            $"{Host}/v10/finance/quoteSummary/{Uri.EscapeDataString(symbol)}?modules=assetProfile,fundProfile&crumb={Uri.EscapeDataString(crumb)}");
        using var doc = JsonDocument.Parse(txt);
        var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return null;
        var r = result[0];
        var p = r.TryGetProperty("assetProfile", out var a) ? a : default;
        string Get(string n) => p.ValueKind == JsonValueKind.Object ? S(p, n) : null;
        string category = r.TryGetProperty("fundProfile", out var f) ? S(f, "categoryName") : null;
        long emp = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("fullTimeEmployees", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0;
        return new Profile(Get("sector"), Get("industry"), emp, Get("website"), Get("longBusinessSummary"), category);
    }

    // Yahoo 업종(GICS 11개) 한국어
    public static string SectorKo(string s) => s switch
    {
        "Technology" => "정보기술", "Communication Services" => "커뮤니케이션", "Consumer Cyclical" => "경기소비재",
        "Consumer Defensive" => "필수소비재", "Healthcare" => "헬스케어", "Financial Services" => "금융",
        "Industrials" => "산업재", "Energy" => "에너지", "Basic Materials" => "소재", "Utilities" => "유틸리티",
        "Real Estate" => "부동산", null => null, _ => s,
    };

    public static async Task<(List<Quote> quotes, double usdKrw)> Fetch()
    {
        await EnsureCrumb();
        double fx = await UsdKrw();
        if (fx <= 0) throw new Exception("환율을 못 받았어요.");
        var list = new List<Quote>();
        await Screen("EQUITY", "intradaymarketcap", 2e9, fx, list);
        await Screen("ETF", "fundnetassets", 1e9, fx, list);
        // 같은 심볼이 두 번 오는 경우 제거
        return (list.GroupBy(q => q.Code).Select(g => g.First()).ToList(), fx);
    }
}
