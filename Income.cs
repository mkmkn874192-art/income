using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace KrxScreen;

// 배당 수입 목록 한 줄. Dps = 연간 주당배당금(원), Source = 출처 설명
public class IncomeItem
{
    public string Code { get; set; }
    public string Name { get; set; }
    public double Qty { get; set; }
    public double Price { get; set; }
    public double Dps { get; set; }
    public string Source { get; set; }
    public string Currency { get; set; } = "KRW"; // Price·Dps의 통화
}

public static class Income
{
    public const double TaxRate = KrTax.DividendTax;             // 국내 배당소득세 14% + 지방소득세 1.4%
    public const double UsTaxRate = KrTax.UsDividendWithholding; // 미국 배당 원천징수 15% (한·미 조세조약)

    public static double Tax(string currency) => currency == "USD" ? UsTaxRate : TaxRate;

    static string File_ => Path.Combine(Market.Dir, "income.json");
    static string CorpFile => Path.Combine(Market.Dir, "corpcodes.json");

    public static List<IncomeItem> Load()
    {
        try { return File.Exists(File_) ? JsonSerializer.Deserialize<List<IncomeItem>>(File.ReadAllText(File_)) : new(); }
        catch { return new(); }
    }

    public static void Save(List<IncomeItem> items)
    {
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(items));
    }

    // 종목코드 -> DART 고유번호. corpCode.xml(약 3.6MB)은 하루 한 번만 받는다.
    static Dictionary<string, string> corpMap;

    static async Task<Dictionary<string, string>> CorpMap(string dartKey, HttpClient http)
    {
        if (corpMap != null) return corpMap;
        if (File.Exists(CorpFile) && File.GetLastWriteTime(CorpFile) > DateTime.Now.AddDays(-1))
            return corpMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CorpFile));

        var bytes = await http.GetByteArrayAsync($"https://opendart.fss.or.kr/api/corpCode.xml?crtfc_key={dartKey}");
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
            Directory.CreateDirectory(Market.Dir);
            File.WriteAllText(CorpFile, JsonSerializer.Serialize(map));
            return corpMap = map;
        }
        catch (InvalidDataException)
        {
            throw new Exception("OpenDART 응답 오류: " + System.Text.Encoding.UTF8.GetString(bytes.Take(300).ToArray()));
        }
    }

    // DART 기업개황: 대표자, 설립일(yyyyMMdd), 홈페이지
    public record Company(string Ceo, string Established, string Homepage);

    public static async Task<Company> CompanyInfo(string dartKey, string code)
    {
        using var http = Screener.NewHttp();
        var map = await CorpMap(dartKey, http);
        string baseCode = code.EndsWith("0") ? code : code[..5] + "0";
        if (!map.TryGetValue(baseCode, out var corp)) return null;
        using var doc = JsonDocument.Parse(await http.GetStringAsync(
            $"https://opendart.fss.or.kr/api/company.json?crtfc_key={dartKey}&corp_code={corp}"));
        var r = doc.RootElement;
        string S(string n) => r.TryGetProperty(n, out var p) ? p.GetString()?.Trim() : null;
        return S("status") == "000" ? new Company(S("ceo_nm"), S("est_dt"), S("hm_url")) : null;
    }

    // 최근 사업보고서의 보통주 주당 현금배당금. 우선주 코드는 우선주 배당을 찾는다. 못 찾으면 null (ETF 등).
    public static async Task<(double dps, int year)?> LatestDps(string dartKey, string code)
    {
        using var http = Screener.NewHttp();
        var map = await CorpMap(dartKey, http);
        bool pref = !code.EndsWith("0");
        string baseCode = pref ? code[..5] + "0" : code; // 우선주는 보통주 회사로 조회
        if (!map.TryGetValue(baseCode, out var corp)) return null;

        for (int y = DateTime.Today.Year - 1; y >= DateTime.Today.Year - 2; y--)
        {
            var txt = await http.GetStringAsync(
                $"https://opendart.fss.or.kr/api/alotMatter.json?crtfc_key={dartKey}&corp_code={corp}&bsns_year={y}&reprt_code=11011");
            using var doc = JsonDocument.Parse(txt);
            if (!doc.RootElement.TryGetProperty("list", out var list)) continue;
            foreach (var row in list.EnumerateArray())
            {
                string se = row.TryGetProperty("se", out var a) ? a.GetString() : "";
                string kind = row.TryGetProperty("stock_knd", out var b) ? b.GetString() ?? "" : "";
                if (!se.Contains("주당 현금배당금") || !kind.Contains(pref ? "우선" : "보통")) continue;
                string v = row.TryGetProperty("thstrm", out var c) ? (c.GetString() ?? "").Replace(",", "").Trim() : "";
                if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var dps)) return (dps, y);
                return (0, y); // '-' = 배당 없음
            }
        }
        return null;
    }
}
