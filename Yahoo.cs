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

    // Yahoo 업종(GICS 11개). screener에서 업종으로 걸러 받으면 종목마다 업종을 알 수 있다.
    public static readonly string[] Sectors =
    {
        "Technology", "Communication Services", "Consumer Cyclical", "Consumer Defensive", "Healthcare",
        "Financial Services", "Industrials", "Energy", "Basic Materials", "Utilities", "Real Estate",
    };

    // 추세 지표 (모두 %): 1년 등락, 50일선·200일선 대비, 52주 고점 대비
    public record Trend(string Sector, double? Chg1y, double? Vs50, double? Vs200, double? FromHigh);

    static double? Pct(JsonElement q, string name, double mul) =>
        q.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() * mul : null;

    static Trend TrendOf(JsonElement q, string sector) => new(sector,
        Pct(q, "fiftyTwoWeekChangePercent", 1),            // 이 항목만 % 단위
        Pct(q, "fiftyDayAverageChangePercent", 100),       // 나머지는 비율(-0.15 = -15%)
        Pct(q, "twoHundredDayAverageChangePercent", 100),
        Pct(q, "fiftyTwoWeekHighChangePercent", 100));

    // region(us/kr), quoteType(EQUITY/ETF)로 sortField 큰 순서대로 전부 받는다. min이 있으면 그 값 초과만, sector가 있으면 그 업종만.
    static async Task ScreenAll(string region, string quoteType, string sortField, double? min, string sector, Action<JsonElement> each)
    {
        for (int offset = 0; offset < 10_000; offset += 250)
        {
            var ops = new List<object> { new { @operator = "eq", operands = new object[] { "region", region } } };
            if (min != null) ops.Add(new { @operator = "gt", operands = new object[] { sortField, min.Value } });
            if (sector != null) ops.Add(new { @operator = "eq", operands = new object[] { "sector", sector } });
            var body = JsonSerializer.Serialize(new
            {
                size = 250, offset, sortField, sortType = "DESC", quoteType,
                query = new { @operator = "AND", operands = ops },
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
            foreach (var q in quotes.EnumerateArray()) each(q);
            int total = result.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
            if (quotes.GetArrayLength() < 250 || offset + 250 >= total) break;
        }
    }

    static Quote UsQuote(JsonElement q, bool etf, string sector, double fx)
    {
        double price = D(q, "regularMarketPrice");
        double yield = D(q, "dividendYield"); // % 단위
        double size = etf ? D(q, "netAssets") : D(q, "marketCap");
        var tr = TrendOf(q, sector);
        return new Quote(S(q, "symbol"), S(q, "shortName") ?? S(q, "longName") ?? S(q, "symbol"),
            etf ? "US ETF" : "US", price, D(q, "regularMarketChange"), D(q, "regularMarketChangePercent"),
            D(q, "regularMarketVolume") * price * fx, size * fx)
        {
            Currency = "USD",
            DivYield = yield > 0 ? yield : null,
            Dps = yield > 0 ? price * yield / 100 : null,
            Sector = sector, Chg1y = tr.Chg1y, Vs50 = tr.Vs50, Vs200 = tr.Vs200, FromHigh = tr.FromHigh,
            Per = !etf && D(q, "trailingPE") > 0 ? D(q, "trailingPE") : null,
            Pbr = !etf && D(q, "priceToBook") > 0 ? D(q, "priceToBook") : null,
        };
    }

    // 국장 전 종목의 업종·추세 (가격·이름은 공공데이터 쪽을 쓰고 이것만 덧붙인다). 키 = 6자리 종목코드
    public static async Task<Dictionary<string, Trend>> KrTrends()
    {
        await EnsureCrumb();
        var map = new Dictionary<string, Trend>();
        foreach (var sector in Sectors)
            await ScreenAll("kr", "EQUITY", "intradaymarketcap", null, sector, q =>
            {
                string sym = S(q, "symbol") ?? "";
                int dot = sym.IndexOf('.');
                if (dot == 6) map[sym[..6]] = TrendOf(q, sector);
            });
        return map;
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

    // 여러 심볼의 숫자 항목 전부 (지수·금리·환율용)
    public static async Task<Dictionary<string, Dictionary<string, double>>> QuoteFields(IEnumerable<string> symbols)
    {
        await EnsureCrumb();
        var txt = await http.GetStringAsync(
            $"{Host}/v7/finance/quote?symbols={Uri.EscapeDataString(string.Join(",", symbols))}&crumb={Uri.EscapeDataString(crumb)}");
        using var doc = JsonDocument.Parse(txt);
        var map = new Dictionary<string, Dictionary<string, double>>();
        foreach (var q in doc.RootElement.GetProperty("quoteResponse").GetProperty("result").EnumerateArray())
            map[S(q, "symbol")] = q.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number)
                                   .ToDictionary(p => p.Name, p => p.Value.GetDouble());
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

    // 최근 10년 월별 종가와 그 달 배당 (시뮬레이터용). 키 = 그 달 1일
    public static async Task<SortedDictionary<DateTime, (double close, double div)>> Monthly(string symbol)
    {
        var map = new SortedDictionary<DateTime, (double, double)>();
        var txt = await http.GetStringAsync($"{Host}/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=10y&interval=1mo&events=div");
        using var doc = JsonDocument.Parse(txt);
        var result = doc.RootElement.GetProperty("chart").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return map;
        var r = result[0];
        if (!r.TryGetProperty("timestamp", out var ts)) return map;
        var close = r.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");
        DateTime Month(long unix) { var d = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.AddHours(12); return new DateTime(d.Year, d.Month, 1); }
        for (int i = 0; i < ts.GetArrayLength(); i++)
            if (close[i].ValueKind == JsonValueKind.Number) map[Month(ts[i].GetInt64())] = (close[i].GetDouble(), 0);
        // Yahoo 자료에 가끔 엉뚱한 값이 섞인다 (예: 2017.08 원/달러 0.12).
        // 앞뒤 달 모두와 40% 넘게 차이 나는 값은 오류로 보고 뺀다.
        var keys = map.Keys.ToList();
        var bad = new List<DateTime>();
        bool Off(double a, double b) => a > 0 && b > 0 && Math.Abs(Math.Log(a / b)) > Math.Log(1.4);
        for (int i = 1; i < keys.Count - 1; i++)
        {
            double prev = map[keys[i - 1]].Item1, cur = map[keys[i]].Item1, next = map[keys[i + 1]].Item1;
            if (Off(cur, prev) && Off(cur, next) && !Off(prev, next)) bad.Add(keys[i]);
        }
        foreach (var k in bad) map.Remove(k); // 다 본 뒤에 지운다
        if (r.TryGetProperty("events", out var ev) && ev.TryGetProperty("dividends", out var divs))
            foreach (var p in divs.EnumerateObject())
            {
                var m = Month(p.Value.GetProperty("date").GetInt64());
                if (map.TryGetValue(m, out var v)) map[m] = (v.Item1, v.Item2 + p.Value.GetProperty("amount").GetDouble());
            }
        return map;
    }

    // 최근 10년 배당 지급 기록 (지급일, 주당 금액). crumb 없이 된다.
    public static async Task<List<Point>> DividendHistory(string symbol)
    {
        using var resp = await http.GetAsync($"{Host}/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=10y&interval=1mo&events=div");
        if ((int)resp.StatusCode == 429) throw new HttpRequestException("Yahoo 요청 한도", null, HttpStatusCode.TooManyRequests);
        if (!resp.IsSuccessStatusCode) return new();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("chart").GetProperty("result");
        var list = new List<Point>();
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return list;
        if (!result[0].TryGetProperty("events", out var ev) || !ev.TryGetProperty("dividends", out var divs)) return list;
        foreach (var p in divs.EnumerateObject())
            list.Add(new Point(DateTimeOffset.FromUnixTimeSeconds(p.Value.GetProperty("date").GetInt64()).LocalDateTime.Date,
                               p.Value.GetProperty("amount").GetDouble()));
        return list.OrderBy(x => x.Date).ToList();
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
        foreach (var sector in Sectors) // 업종별로 받아야 종목마다 업종이 붙는다
            await ScreenAll("us", "EQUITY", "intradaymarketcap", 2e9, sector, q =>
            {
                if (!(S(q, "fullExchangeName") ?? "").Contains("OTC")) list.Add(UsQuote(q, false, sector, fx)); // 장외 제외
            });
        await ScreenAll("us", "ETF", "fundnetassets", 1e9, null, q =>
        {
            if (!(S(q, "fullExchangeName") ?? "").Contains("OTC")) list.Add(UsQuote(q, true, null, fx));
        });
        // 같은 심볼이 두 번 오는 경우 제거
        return (list.GroupBy(q => q.Code).Select(g => g.First()).ToList(), fx);
    }
}
