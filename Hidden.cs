using System.Text.Json;
using System.Text.RegularExpressions;

namespace KrxScreen;

// 숨은 능력치: 표에는 안 보이고 '스카우트 보고서'를 써야 열리는 회사의 성격.
// 확인할 수 있는 자료로만 매긴다 — DART 최대주주·임원 현황(국내), 10년 월별 가격, 배당 기록.
public static class Hidden
{
    public record Attr(string Name, int? Value, string Hint, bool Neutral = false); // Neutral: 높다고 좋은 게 아닌 성격 (정책 영향)
    record Entry(DateTime At, List<Attr> Attrs);

    static readonly string File_ = Path.Combine(Market.Dir, "hidden.json");
    static Dictionary<string, Entry> cache;
    static Dictionary<string, Entry> Cache
    {
        get
        {
            if (cache != null) return cache;
            try { cache = File.Exists(File_) ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(File_)) : null; } catch { } // 깨졌으면 새로
            return cache ??= new();
        }
    }

    // 정부·공공기관 주주 이름
    static readonly string[] Public = { "대한민국정부", "정부", "기획재정부", "재정경제부", "한국산업은행", "산업은행", "한국수출입은행", "예금보험공사",
                                        "한국투자공사", "국민연금", "공단", "공사" };
    static bool IsPublic(string name) => Public.Any(name.Contains) && !name.Contains("주식회사");

    // 규제가 센 업종 (요금·금리·허가를 정부가 쥔다)
    static bool Regulated(StockRow r) => r.Q.Sector is "Utilities" or "Communication Services" or "Financial Services"
                                         || new[] { "은행", "금융지주", "전력", "가스", "통신", "텔레콤" }.Any(r.Q.Name.Contains);

    // monthly: 10년 월별 종가 (없으면 null)
    public static async Task<List<Attr>> Get(StockRow r, string dartKey, Func<Task<SortedDictionary<DateTime, (double close, double div)>>> monthly)
    {
        string code = r.Q.Code;
        if (Cache.TryGetValue(code, out var e) && e.At > DateTime.Now.AddDays(-30)) return e.Attrs;
        var list = new List<Attr>();
        if (!r.Q.IsUs && !string.IsNullOrWhiteSpace(dartKey))
        {
            try { list.AddRange(await FromDart(r, dartKey)); }
            catch (Exception ex) { list.Add(new("주인 지분", null, "공시를 못 받았어요: " + ex.Message.Split('\n')[0])); }
        }
        else
        {
            list.Add(new("주인 지분", null, r.Q.IsUs ? "미국 종목은 국내 공시(DART) 자료가 없어요" : "DART 키를 넣으면 볼 수 있어요"));
            list.Add(new("정책 영향", Regulated(r) ? 10 : 4, Regulated(r) ? "규제가 센 업종 (요금·금리·허가)" : "규제가 센 업종이 아니에요", true));
            list.Add(new("경영 연속성", null, r.Q.IsUs ? "미국 종목은 국내 공시(DART) 자료가 없어요" : "DART 키를 넣으면 볼 수 있어요"));
        }
        try { list.Add(Recovery(await monthly())); } catch { list.Add(new("위기 회복력", null, "가격 기록을 못 받았어요")); }
        list.Add(Payout(r));
        Cache[code] = new Entry(DateTime.Now, list);
        try { Directory.CreateDirectory(Market.Dir); File.WriteAllText(File_, JsonSerializer.Serialize(Cache)); } catch { } // 저장 실패는 다음에 다시 받으면 된다
        return list;
    }

    static async Task<List<Attr>> FromDart(StockRow r, string key)
    {
        var res = new List<Attr>();
        string corp = await Income.CorpCode(key, r.Q.Code);
        if (corp == null) return new() { new("주인 지분", null, "DART에서 회사를 못 찾았어요 (ETF·우선주 등)") };
        using var http = Screener.NewHttp();

        async Task<JsonElement?> Latest(string api)
        {
            for (int y = DateTime.Today.Year - 1; y >= DateTime.Today.Year - 2; y--)
            {
                var txt = await http.GetStringAsync($"https://opendart.fss.or.kr/api/{api}.json?crtfc_key={key}&corp_code={corp}&bsns_year={y}&reprt_code=11011");
                using var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("list", out var l) && l.GetArrayLength() > 0) return l.Clone();
            }
            return null;
        }
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()?.Trim() ?? "" : "";

        // 1) 최대주주 현황: '계' 줄의 보통주 기말 지분율, 정부·공공 주주
        var holders = await Latest("hyslrSttus");
        if (holders is JsonElement hl)
        {
            var rows = hl.EnumerateArray().Where(x => S(x, "stock_knd").Contains("보통")).ToList();
            double Pct(JsonElement x) => double.TryParse(S(x, "trmend_posesn_stock_qota_rt"), out var v) ? v : 0;
            var total = rows.FirstOrDefault(x => S(x, "nm") == "계");
            double q = total.ValueKind == JsonValueKind.Object ? Pct(total) : rows.Where(x => S(x, "nm") != "계").Sum(Pct);
            var top = rows.FirstOrDefault(x => S(x, "relate").Contains("최대주주") && !S(x, "relate").Contains("특수"));
            string topName = top.ValueKind == JsonValueKind.Object ? S(top, "nm") : rows.FirstOrDefault(x => S(x, "nm") != "계") is { ValueKind: JsonValueKind.Object } f ? S(f, "nm") : "";
            int qv = q < 10 ? 6 : q < 15 ? 8 : q < 25 ? 12 : q < 35 ? 15 : q < 55 ? 17 : q < 70 ? 13 : 9;
            string qNote = q < 15 ? "주인 지분이 적어 경영권이 흔들릴 수 있어요" : q < 25 ? "주인 지분이 보통 수준이에요"
                         : q <= 65 ? "주인 지분이 넉넉해 경영이 안정적인 편이에요" : "주인 지분이 많아 시장에 도는 주식이 적어요";
            res.Add(new("주인 지분", qv, $"최대주주 {topName} 쪽 {q:0.#}% · {qNote}"));

            double gov = rows.Where(x => S(x, "nm") != "계" && IsPublic(S(x, "nm"))).Sum(Pct);
            var govNames = rows.Where(x => S(x, "nm") != "계" && IsPublic(S(x, "nm"))).Select(x => S(x, "nm")).Distinct().ToList();
            if (gov > 0) res.Add(new("정책 영향", Math.Clamp((int)Math.Round(10 + gov / 5), 12, 20), $"정부·공공 지분 {gov:0.#}% ({string.Join(", ", govNames)}) · 배당·요금이 정책에 따라 달라질 수 있어요", true));
            else res.Add(new("정책 영향", Regulated(r) ? 10 : 4, Regulated(r) ? "정부 지분은 없지만 규제가 센 업종 (요금·금리·허가)" : "정부 지분이 없고 규제가 센 업종도 아니에요", true));
        }
        else res.Add(new("주인 지분", null, "최대주주 공시가 없어요"));

        // 2) 임원 현황: 대표의 재직 기간, 최대주주와의 관계(오너 일가 / 전문경영인)
        var execs = await Latest("exctvSttus");
        if (execs is JsonElement el)
        {
            var all = el.EnumerateArray().ToList();
            var ceo = all.FirstOrDefault(x => S(x, "chrg_job").Contains("대표") || S(x, "ofcps").Contains("대표"));
            if (ceo.ValueKind != JsonValueKind.Object)
                ceo = all.FirstOrDefault(x => S(x, "fte_at") == "상근" && Regex.IsMatch(S(x, "ofcps"), "회장|사장|CEO"));
            if (ceo.ValueKind == JsonValueKind.Object)
            {
                int? months = Months(S(ceo, "hffc_pd"));
                string rel = S(ceo, "mxmm_shrholdr_relate");
                bool owner = Regex.IsMatch(rel, "본인|배우자|자녀|친인척|혈족|인척|직계");
                int v = months is int m ? (m >= 120 ? 18 : m >= 60 ? 15 : m >= 24 ? 12 : m >= 12 ? 9 : 6) : 10;
                string tenure = months is int mm ? (mm >= 12 ? $"{mm / 12}년 {mm % 12}개월" : $"{mm}개월") : "기간 모름";
                res.Add(new("경영 연속성", v, $"대표 {S(ceo, "nm")} {S(ceo, "ofcps")} · 재직 {tenure} · {(owner ? "오너 일가 경영" : "전문경영인")}"));
            }
            else res.Add(new("경영 연속성", null, "대표 정보를 못 찾았어요"));
        }
        else res.Add(new("경영 연속성", null, "임원 공시가 없어요"));
        return res;
    }

    // "46개월", "3년 2개월", "'23.09.19~" → 개월 수
    static int? Months(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var ym = Regex.Match(s, @"(\d+)\s*년\s*(\d+)?\s*개?월?");
        if (ym.Success && s.Contains('년')) return int.Parse(ym.Groups[1].Value) * 12 + (ym.Groups[2].Success ? int.Parse(ym.Groups[2].Value) : 0);
        var m = Regex.Match(s, @"(\d+)\s*개월");
        if (m.Success) return int.Parse(m.Groups[1].Value);
        var d = Regex.Match(s, @"'?(\d{2,4})\.(\d{1,2})");
        if (d.Success)
        {
            int y = int.Parse(d.Groups[1].Value); if (y < 100) y += 2000;
            var start = new DateTime(y, Math.Clamp(int.Parse(d.Groups[2].Value), 1, 12), 1);
            return Math.Max(0, (DateTime.Today.Year - start.Year) * 12 + DateTime.Today.Month - start.Month);
        }
        return null;
    }

    // 위기 회복력: 10년 동안 10% 넘게 떨어졌다가 '이미 회복한' 일들 중 가장 깊었던 것이 몇 달 만에 회복됐나.
    // 지금 떨어져 있는 중인 것은 따로 알려 준다 (진행 중인 하락 하나로 10년을 판단하지 않게).
    static Attr Recovery(SortedDictionary<DateTime, (double close, double div)> d)
    {
        if (d == null || d.Count < 24) return new("위기 회복력", null, "가격 기록이 2년이 안 돼요");
        var c = d.Values.Select(v => v.close).Where(x => x > 0).ToList();
        var done = new List<(double depth, int months)>();
        double peak = c[0], low = c[0]; int lowI = 0;
        for (int i = 1; i < c.Count; i++)
        {
            if (c[i] >= peak)
            {
                double depth = low / peak - 1;
                if (depth <= -0.10) done.Add((depth, i - lowI)); // 바닥에서 전 고점까지 걸린 달 수
                peak = low = c[i]; lowI = i;
            }
            else if (c[i] < low) { low = c[i]; lowI = i; }
        }
        double now = c[^1] / peak - 1, nowLow = low / peak - 1;
        string ongoing = nowLow <= -0.10 ? $" · 지금 고점 대비 {now * 100:0}% (진행 중)" : "";
        if (done.Count == 0)
            return nowLow <= -0.10
                ? new("위기 회복력", 7, $"10년 동안 큰 하락은 지금 한 번 — 고점 대비 최대 {nowLow * 100:0}%, 아직 회복 중이에요")
                : new("위기 회복력", 18, $"10년 동안 10% 넘게 떨어진 적이 없어요");
        var worst = done.OrderBy(x => x.depth).First();
        int v = worst.months <= 6 ? 18 : worst.months <= 12 ? 16 : worst.months <= 24 ? 13 : worst.months <= 48 ? 10 : 7;
        v -= worst.depth < -0.6 ? 4 : worst.depth < -0.4 ? 2 : 0;
        if (nowLow < worst.depth) v = Math.Min(v - 2, nowLow < -0.6 ? 4 : nowLow < -0.4 ? 7 : 10); // 지금 하락이 지난 최악보다 깊으면 그만큼 낮춘다
        return new("위기 회복력", Math.Clamp(v, 1, 20),
            $"지난 최대 하락 {worst.depth * 100:0}% → {worst.months}개월 만에 전 고점 회복 (10% 넘는 하락 {done.Count}번 모두 회복){ongoing}");
    }

    // 주주 환원: 배당을 얼마나 오래, 얼마나 늘려 왔나
    static Attr Payout(StockRow r)
    {
        if (r.DivI is not { } d || d.Freq == "없음") return new("주주 환원", r.Yield > 0 ? 8 : 3, r.Yield > 0 ? "배당 기록이 짧아요" : "배당을 주지 않아요");
        int v = 6 + Math.Min(d.Years, 10) + (d.Growth3y is double g ? (g >= 10 ? 4 : g >= 3 ? 2 : g < 0 ? -3 : 0) : 0) - d.Cuts * 2;
        return new("주주 환원", Math.Clamp(v, 1, 20), $"연속 {d.Years}년 배당" + (d.Growth3y is double g2 ? $" · 3년간 배당 연 {g2:+0;-0}%" : "") + (d.Cuts > 0 ? $" · 최근 5년 {d.Cuts}번 줄임" : ""));
    }
}
