using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace KrxScreen;

/// <summary>
/// 국장 가치투자 1차 스크리닝 (그레이엄 숫자 기준 + 배당 가중).
/// 공공데이터포털 '금융위원회_주식시세정보' + OpenDART.
/// </summary>
public class Screener
{
    const string Dart = "https://opendart.fss.or.kr/api";
    static readonly int[] FinYears = Enumerable.Range(2015, 11).ToArray(); // 2015~2025
    static readonly int[] DivYears = { 2025, 2022, 2019, 2016 };           // 각 보고서 3개년 -> 2014~2025

    readonly HttpClient http = NewHttp();

    // OpenDART는 User-Agent가 없으면 302(error1.html)로 돌려보낸다
    public static HttpClient NewHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("KrxScreen/1.0");
        return h;
    }
    readonly string goKey, dartKey;
    readonly double minPrice, minCap;
    readonly IProgress<string> log;

    // 종목코드 -> 연도별 순이익·주당배당금 (상세 패널 차트용)
    public Dictionary<string, StockSeries> Series { get; } = new();

    public Screener(string goKey, string dartKey, double minPrice, double minCap, IProgress<string> log)
    {
        this.goKey = goKey.Trim();
        this.dartKey = dartKey.Trim();
        this.minPrice = minPrice;
        this.minCap = minCap;
        this.log = log;
    }

    record Stock(string Code, string Name, string Market, double Price, double Cap)
    {
        public string Corp { get; set; }
    }

    static double? Num(string s)
    {
        if (s == null) return null;
        s = s.Replace(",", "").Trim();
        if (s == "" || s == "-") return null;
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    static IEnumerable<JsonElement> Items(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return arr.EnumerateArray().ToList();
        return Enumerable.Empty<JsonElement>();
    }

    // DART status: 000 정상, 013 데이터 없음. 그 외(010 키 오류, 020 호출 한도 초과 등)는 결과가 조용히 비지 않게 중단
    static void CheckDart(JsonElement root)
    {
        string st = Str(root, "status");
        if (st != "000" && st != "013")
            throw new Exception($"OpenDART 오류 {st}: {Str(root, "message")}");
    }

    // ---------- 1. 시세 ----------
    async Task<List<Stock>> GetPrices()
    {
        var (d, quotes) = await Market.Fetch(goKey, http);
        log.Report($"시세 기준일 {d}, {quotes.Count}개 종목");
        return quotes.Where(q => q.Code.EndsWith("0") && q.Price >= minPrice && q.Cap >= minCap) // 우선주 제외
                     .Select(q => new Stock(q.Code, q.Name, q.Market, q.Price, q.Cap)).ToList();
    }

    // ---------- 2. DART 고유번호 ----------
    async Task<Dictionary<string, string>> GetCorpCodes()
    {
        var bytes = await http.GetByteArrayAsync($"{Dart}/corpCode.xml?crtfc_key={dartKey}");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            using var s = zip.Entries[0].Open();
            var map = new Dictionary<string, string>();
            foreach (var e in XDocument.Load(s).Descendants("list"))
            {
                string sc = ((string)e.Element("stock_code") ?? "").Trim();
                if (sc != "") map[sc] = (string)e.Element("corp_code");
            }
            return map;
        }
        catch (InvalidDataException)
        {
            // 키가 틀리면 zip 대신 에러 메시지가 옴
            throw new Exception("OpenDART 응답 오류: " + System.Text.Encoding.UTF8.GetString(bytes.Take(300).ToArray()));
        }
    }

    // ---------- 3. 재무 (연결 우선) ----------
    async Task<Dictionary<string, Dictionary<int, Dictionary<string, double?>>>> GetFinancials(List<string> corps)
    {
        // corp -> year -> fs(CFS/OFS) -> 계정 -> 금액
        var raw = new Dictionary<string, Dictionary<int, Dictionary<string, Dictionary<string, double?>>>>();
        foreach (int y in FinYears)
        {
            for (int i = 0; i < corps.Count; i += 100)
            {
                var batch = string.Join(",", corps.Skip(i).Take(100));
                var txt = await http.GetStringAsync(
                    $"{Dart}/fnlttMultiAcnt.json?crtfc_key={dartKey}&corp_code={batch}&bsns_year={y}&reprt_code=11011");
                using var doc = JsonDocument.Parse(txt);
                CheckDart(doc.RootElement);
                foreach (var row in Items(doc.RootElement, "list"))
                {
                    string c = Str(row, "corp_code"), fs = Str(row, "fs_div");
                    if (!raw.TryGetValue(c, out var ym)) raw[c] = ym = new();
                    if (!ym.TryGetValue(y, out var fm)) ym[y] = fm = new();
                    if (!fm.TryGetValue(fs, out var acc)) fm[fs] = acc = new();
                    acc[Str(row, "account_nm")] = Num(Str(row, "thstrm_amount"));
                }
                await Task.Delay(200);
            }
            log.Report($"재무 {y}년 완료");
        }
        var flat = new Dictionary<string, Dictionary<int, Dictionary<string, double?>>>();
        foreach (var (c, ym) in raw)
        {
            flat[c] = new();
            foreach (var (y, fm) in ym)
                flat[c][y] = fm.GetValueOrDefault("CFS") ?? fm.GetValueOrDefault("OFS") ?? new();
        }
        return flat;
    }

    // ---------- 4. 배당 ----------
    async Task<(Dictionary<int, double> dps, double? yield)> GetDividends(string corp)
    {
        var dps = new Dictionary<int, double>();
        double? latestYield = null;
        foreach (int y in DivYears)
        {
            var txt = await http.GetStringAsync(
                $"{Dart}/alotMatter.json?crtfc_key={dartKey}&corp_code={corp}&bsns_year={y}&reprt_code=11011");
            using var doc = JsonDocument.Parse(txt);
            CheckDart(doc.RootElement);
            foreach (var row in Items(doc.RootElement, "list"))
            {
                string se =Str(row, "se") ?? "", kind = Str(row, "stock_knd") ?? "";
                if (kind != "" && !kind.Contains("보통")) continue;
                if (se.Contains("주당 현금배당금"))
                {
                    var fields = new[] { "thstrm", "frmtrm", "lwfr" };
                    for (int off = 0; off < 3; off++)
                    {
                        var v = Num(Str(row, fields[off]));
                        if (v != null && !dps.ContainsKey(y - off)) dps[y - off] = v.Value;
                    }
                }
                if (se.Contains("현금배당수익률") && y == DivYears[0])
                    latestYield = Num(Str(row, "thstrm"));
            }
            await Task.Delay(100);
        }
        return (dps, latestYield);
    }

    static double? First(Dictionary<string, double?> d, params string[] names)
    {
        foreach (var n in names)
            if (d.TryGetValue(n, out var v) && v != null) return v;
        return null;
    }

    // ---------- 실행 ----------
    public async Task<DataTable> Run()
    {
        var pick = await GetPrices();
        log.Report($"필터 통과 {pick.Count}개 (주가 {minPrice:N0}원↑, 시총 {minCap / 1e12:0.#}조↑)");

        var cmap = await GetCorpCodes();
        foreach (var p in pick) p.Corp = cmap.GetValueOrDefault(p.Code);
        pick = pick.Where(p => p.Corp != null).ToList();
        var fin = await GetFinancials(pick.Select(p => p.Corp).ToList());

        var t = new DataTable();
        t.Columns.Add("점수", typeof(int));
        t.Columns.Add("종목명", typeof(string));
        t.Columns.Add("종목코드", typeof(string));
        t.Columns.Add("시장", typeof(string));
        t.Columns.Add("배당수익률(%)", typeof(double));
        t.Columns.Add("PER", typeof(double));
        t.Columns.Add("PER(3년)", typeof(double));
        t.Columns.Add("PBR", typeof(double));
        t.Columns.Add("ROE(%)", typeof(double));
        t.Columns.Add("주가", typeof(double));
        t.Columns.Add("시총(억)", typeof(double));
        t.Columns.Add("부채비율(%)", typeof(double));
        t.Columns.Add("유동비율", typeof(double));
        t.Columns.Add("이익성장(%)", typeof(double));
        t.Columns.Add("재무연수", typeof(int));
        t.Columns.Add("적자연수", typeof(int));
        t.Columns.Add("배당연수", typeof(int));
        string[] checkCols = { "무적자", "배당 꾸준", "이익 성장", "재무 건전", "PER(3년)≤15", "PER×PBR≤22.5", "ROE≥10%" };
        foreach (var c in checkCols) t.Columns.Add(c, typeof(string));

        int n = 0;
        foreach (var p in pick)
        {
            var f = fin.GetValueOrDefault(p.Corp) ?? new();
            var ni = f.Where(kv => First(kv.Value, "당기순이익", "당기순이익(손실)") != null)
                      .ToDictionary(kv => kv.Key, kv => First(kv.Value, "당기순이익", "당기순이익(손실)").Value);
            int last = f.Count > 0 ? f.Keys.Max() : 0;
            var cur = f.GetValueOrDefault(last) ?? new();
            double? equity = First(cur, "자본총계"), debt = First(cur, "부채총계");
            double? ca = First(cur, "유동자산"), cl = First(cur, "유동부채");
            double? niLast = ni.TryGetValue(last, out var nl) ? nl : null;

            double? per = niLast > 0 ? p.Cap / niLast : null;
            // 『현명한 투자자』: 주가 ÷ 최근 3년 평균 이익. 한 해 반짝 실적에 속지 않게 한다.
            // (원래는 주당순이익 기준. 시총 ÷ 순이익은 그동안 주식 수가 크게 안 바뀌었다고 보는 근사치)
            double? per3 = null;
            if (new[] { last, last - 1, last - 2 }.All(ni.ContainsKey))
            {
                double avg3 = (ni[last] + ni[last - 1] + ni[last - 2]) / 3;
                if (avg3 > 0) per3 = p.Cap / avg3;
            }
            double? pbr = equity > 0 ? p.Cap / equity : null;
            double? roe = niLast != null && equity > 0 ? niLast / equity * 100 : null;
            double? curRatio = ca != null && cl > 0 ? ca / cl : null;
            double? debtRatio = debt != null && equity > 0 ? debt / equity * 100 : null;
            var ys = ni.Keys.OrderBy(y => y).ToList();
            int loss = ys.Count(y => ni[y] < 0);
            double? growth = null;
            if (ys.Count >= 6)
            {
                double a = ys.Take(3).Average(y => ni[y]), b = ys.TakeLast(3).Average(y => ni[y]);
                if (a > 0) growth = (b / a - 1) * 100;
            }

            var (dps, dy) = await GetDividends(p.Corp);
            int divYears = dps.Values.Count(v => v > 0);
            Series[p.Code] = new StockSeries(ni, dps);

            bool[] checks =
            {
                ys.Count >= 8 && loss == 0,
                divYears >= 10,
                growth >= 33,
                curRatio != null ? curRatio >= 2 : debtRatio <= 100,
                per3 <= 15,
                per3 != null && pbr != null && per3 * pbr <= 22.5, // 그레이엄의 PER×PBR도 3년 평균 PER 기준
                roe >= 10,
            };
            int score = checks.Count(c => c) + ((dy ?? 0) >= 5 ? 2 : (dy ?? 0) >= 3 ? 1 : 0);

            var r = t.NewRow();
            r["점수"] = score;
            r["종목명"] = p.Name;
            r["종목코드"] = p.Code;
            r["시장"] = p.Market;
            Set(r, "배당수익률(%)", dy);
            Set(r, "PER", per, 1);
            Set(r, "PER(3년)", per3, 1);
            Set(r, "PBR", pbr, 2);
            Set(r, "ROE(%)", roe, 1);
            r["주가"] = p.Price;
            r["시총(억)"] = Math.Round(p.Cap / 1e8);
            Set(r, "부채비율(%)", debtRatio, 0);
            Set(r, "유동비율", curRatio, 2);
            Set(r, "이익성장(%)", growth, 0);
            r["재무연수"] = ys.Count;
            r["적자연수"] = loss;
            r["배당연수"] = divYears;
            for (int i = 0; i < checks.Length; i++) r[checkCols[i]] = checks[i] ? "O" : "X";
            t.Rows.Add(r);

            if (++n % 10 == 0) log.Report($"배당 수집 {n}/{pick.Count}");
        }
        t.DefaultView.Sort = "점수 DESC, [배당수익률(%)] DESC";
        log.Report($"완료: {t.Rows.Count}개 종목");
        return t;
    }

    static void Set(DataRow r, string col, double? v, int digits = -1)
    {
        if (v == null) { r[col] = DBNull.Value; return; }
        r[col] = digits < 0 ? v.Value : Math.Round(v.Value, digits);
    }
}
