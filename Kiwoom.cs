using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KrxScreen;

// 키움 REST API. 토큰(au10001)은 공식 가이드 기준, 나머지는 모의투자 응답으로 검증 필요.
public class Kiwoom
{
    public const string RealDomain = "https://api.kiwoom.com";
    public const string MockDomain = "https://mockapi.kiwoom.com";

    readonly HttpClient http = Screener.NewHttp();
    readonly string domain, appKey, secretKey;
    string token;

    public bool IsMock => domain == MockDomain;

    public Kiwoom(bool mock, string appKey, string secretKey)
    {
        domain = mock ? MockDomain : RealDomain;
        this.appKey = appKey.Trim();
        this.secretKey = secretKey.Trim();
    }

    public record Holding(string Code, string Name, long Qty, double AvgPrice, double CurPrice, double EvalAmt);

    public static double Num(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return double.TryParse(s.Replace(",", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    static void Check(JsonElement root, string what, string raw)
    {
        string rc = Str(root, "return_code");
        if (rc != null && rc != "0")
            throw new Exception($"키움 {what} 실패 ({rc}): {Str(root, "return_msg")}");
        if (rc == null)
            throw new Exception($"키움 {what} 응답에 return_code가 없어요.\r\n응답 앞부분: {(raw.Length > 300 ? raw[..300] : raw)}");
    }

    // au10001 접근토큰 발급
    async Task EnsureToken()
    {
        if (token != null) return;
        var body = JsonSerializer.Serialize(new { grant_type = "client_credentials", appkey = appKey, secretkey = secretKey });
        using var resp = await http.PostAsync(domain + "/oauth2/token",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var txt = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(txt);
        Check(doc.RootElement, "토큰 발급", txt);
        token = Str(doc.RootElement, "token");
    }

    // POST + api-id 헤더. cont-yn/next-key로 이어받기.
    async Task<List<JsonElement>> Post(string path, string apiId, object body, string what)
    {
        await EnsureToken();
        var pages = new List<JsonElement>();
        string contYn = "N", nextKey = "";
        for (int i = 0; i < 20; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, domain + path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("api-id", apiId);
            req.Headers.Add("cont-yn", contYn);
            req.Headers.Add("next-key", nextKey);
            using var resp = await http.SendAsync(req);
            var txt = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(txt);
            Check(doc.RootElement, what, txt);
            pages.Add(doc.RootElement.Clone());

            contYn = resp.Headers.TryGetValues("cont-yn", out var c) ? c.First() : "N";
            nextKey = resp.Headers.TryGetValues("next-key", out var k) ? k.First() : "";
            if (contYn != "Y") break;
            await Task.Delay(400); // kt00018 초당 3회 제한
        }
        return pages;
    }

    // kt00018 계좌평가잔고내역
    public async Task<List<Holding>> GetHoldings()
    {
        var pages = await Post("/api/dostk/acnt", "kt00018", new { qry_tp = "1", dmst_stex_tp = "KRX" }, "잔고 조회");
        var list = new List<Holding>();
        foreach (var page in pages)
        {
            if (!page.TryGetProperty("acnt_evlt_remn_indv_tot", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var r in arr.EnumerateArray())
            {
                string code = Str(r, "stk_cd") ?? "";
                if (code.Length == 7 && char.IsLetter(code[0])) code = code[1..]; // A005930 -> 005930
                list.Add(new Holding(code, (Str(r, "stk_nm") ?? "").Trim(),
                    (long)Num(Str(r, "rmnd_qty")), Num(Str(r, "pur_pric")),
                    Math.Abs(Num(Str(r, "cur_prc"))), Num(Str(r, "evlt_amt"))));
            }
        }
        return list;
    }

    // kt00001 예수금상세현황: (예수금, 주문가능금액)
    public async Task<(double entr, double orderable)> GetCash()
    {
        var page = (await Post("/api/dostk/acnt", "kt00001", new { qry_tp = "3" }, "예수금 조회"))[0];
        return (Num(Str(page, "entr")), Num(Str(page, "ord_alow_amt")));
    }

    // kt10000 매수 (지정가 trde_tp=0). 성공하면 주문번호 반환. 접수일 뿐 체결이 아님.
    public async Task<string> Buy(string code, long qty, long price)
    {
        var body = new
        {
            dmst_stex_tp = "KRX",
            stk_cd = code,
            ord_qty = qty.ToString(),
            ord_uv = price.ToString(),
            trde_tp = "0",
        };
        var page = (await Post("/api/dostk/ordr", "kt10000", body, $"매수 주문 {code}"))[0];
        return Str(page, "ord_no");
    }
}
