using System.Globalization;
using System.Text.Json;

namespace KrxScreen;

// 지금 시장 환경: 금리(한국은행 ECOS·Yahoo), 장단기 금리차, 환율, 지수 위치.
// 해석은 '이 숫자면 보통 이렇게 본다'는 규칙이지 예측이 아니다.
public static class Macro
{
    public record Index(string Name, double Price, double Chg1y, double FromHigh, double Vs200);

    public class Snapshot
    {
        public double? Ktb3, Ktb3YearAgo, Ktb10, BaseRate; // 국내 금리 %
        public double Us10, Us3m;                          // 미국 금리 %
        public double Fx, FxLow, FxHigh;                   // 원/달러와 1년 범위
        public double SchdYield, SpyPe;
        public List<Index> Indexes = new();
        public DateTime At = DateTime.Now;
        public Index Get(string name) => Indexes.FirstOrDefault(i => i.Name == name);
    }

    public static Snapshot Last;

    // ECOS: 마지막 값 (start~end 사이 일별 자료 중 마지막)
    static async Task<double?> Ecos(HttpClient http, string key, string stat, string item, DateTime end)
    {
        string url = $"https://ecos.bok.or.kr/api/StatisticSearch/{key}/json/kr/1/10/{stat}/D/{end.AddDays(-14):yyyyMMdd}/{end:yyyyMMdd}/{item}";
        using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("StatisticSearch", out var s) || !s.TryGetProperty("row", out var rows)) return null;
        var last = rows.EnumerateArray().LastOrDefault();
        return last.ValueKind == JsonValueKind.Object && double.TryParse(last.GetProperty("DATA_VALUE").GetString(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public static async Task<Snapshot> Fetch(string ecosKey)
    {
        var s = new Snapshot();
        var q = await Yahoo.QuoteFields(new[] { "^TNX", "^IRX", "^GSPC", "^KS11", "^KQ11", "KRW=X", "SPY", "SCHD" });
        double F(string sym, string f) => q.TryGetValue(sym, out var d) && d.TryGetValue(f, out var v) ? v : 0;
        s.Us10 = F("^TNX", "regularMarketPrice");
        s.Us3m = F("^IRX", "regularMarketPrice");
        s.Fx = F("KRW=X", "regularMarketPrice");
        s.FxLow = F("KRW=X", "fiftyTwoWeekLow");
        s.FxHigh = F("KRW=X", "fiftyTwoWeekHigh");
        s.SchdYield = F("SCHD", "dividendYield");
        s.SpyPe = F("SPY", "trailingPE");
        foreach (var (sym, name) in new[] { ("^KS11", "코스피"), ("^KQ11", "코스닥"), ("^GSPC", "S&P500") })
            s.Indexes.Add(new Index(name, F(sym, "regularMarketPrice"), F(sym, "fiftyTwoWeekChangePercent"),
                F(sym, "fiftyTwoWeekHighChangePercent") * 100, F(sym, "twoHundredDayAverageChangePercent") * 100));

        // 국내 금리는 실패해도 나머지는 보여준다
        try
        {
            using var http = Screener.NewHttp();
            string key = string.IsNullOrWhiteSpace(ecosKey) ? "sample" : ecosKey.Trim();
            s.Ktb3 = await Ecos(http, key, "817Y002", "010200000", DateTime.Today);
            s.Ktb3YearAgo = await Ecos(http, key, "817Y002", "010200000", DateTime.Today.AddYears(-1));
            s.Ktb10 = await Ecos(http, key, "817Y002", "010210000", DateTime.Today);
            s.BaseRate = await Ecos(http, key, "722Y001", "0101000", DateTime.Today);
        }
        catch { }
        return Last = s;
    }

    public record Signal(string Title, string Value, string Meaning, int Tilt); // Tilt: 채권(+)/주식(-) 쪽으로 기우는 정도

    public static List<Signal> Read(Snapshot s)
    {
        var list = new List<Signal>();

        if (s.Ktb3 is double k3)
        {
            string chg = s.Ktb3YearAgo is double a ? $" (1년 전 {a:0.00}% → {k3 - a:+0.00;-0.00}%p)" : "";
            bool up = s.Ktb3YearAgo is double b && k3 > b + 0.25;
            list.Add(new Signal("국내 금리", $"국고채 3년 {k3:0.00}%{chg}" + (s.BaseRate is double br ? $" · 기준금리 {br:0.00}%" : ""),
                up ? "1년 전보다 금리가 올라 채권 이자 매력이 커졌어요. 앞으로 금리가 내리면 채권 ETF 가격도 오를 여지가 있어요."
                   : "금리가 1년 전과 비슷하거나 낮아요. 채권은 이자보다 '버팀목' 역할로 보는 게 맞아요.",
                up ? 1 : 0));
        }
        else list.Add(new Signal("국내 금리", "받지 못했어요", "설정에서 한국은행 ECOS 키를 넣으면 국고채 금리를 보여줘요.", 0));

        double spread = s.Us10 - s.Us3m;
        list.Add(new Signal("장단기 금리차", $"미국 10년 {s.Us10:0.00}% − 3개월 {s.Us3m:0.00}% = {spread:+0.00;-0.00}%p",
            spread < 0 ? "역전 상태예요. 경기 둔화 신호로 자주 언급돼요. 채권 비중을 목표보다 줄이지 않는 게 좋아요."
                       : "정상(양수)이에요. 경기 둔화 신호는 아니에요.",
            spread < 0 ? 1 : 0));

        if (s.SchdYield > 0)
        {
            double cmp = s.Ktb3 ?? s.Us10;
            string what = s.Ktb3 != null ? "국고채 3년" : "미국 10년";
            list.Add(new Signal("배당 vs 채권", $"미국배당 ETF {s.SchdYield:0.00}% · {what} {cmp:0.00}%",
                s.SchdYield < cmp
                    ? "지금은 '이자'만 보면 채권이 배당보다 높아요. 배당주는 배당이 늘어나는 것과 주가 상승까지 기대할 때 의미가 있어요."
                    : "배당 ETF의 배당률이 채권 금리보다 높아요. 현금 흐름만 보면 배당 쪽이 유리해요.",
                s.SchdYield < cmp ? 1 : -1));
        }

        foreach (var i in s.Indexes)
        {
            string state = i.FromHigh <= -20 ? "약세장(고점 대비 -20% 이상)" : i.FromHigh <= -10 ? "조정(고점 대비 -10% 이상)" : "고점 근처";
            list.Add(new Signal($"{i.Name} 위치", $"고점 대비 {i.FromHigh:0.0}% · 1년 {i.Chg1y:+0.0;-0.0}% · 200일선 대비 {i.Vs200:+0.0;-0.0}%",
                i.FromHigh <= -20
                    ? $"{state}이에요. 적립식으로는 같은 돈으로 더 많은 수량을 사는 구간이에요. 비중 맞추기로 부족해진 주식 몫을 채우는 때예요."
                    : i.FromHigh <= -10 ? $"{state}이에요. 계획대로 적립을 이어가면 돼요."
                    : $"{state}예요. 한 번에 많이 사기보다 나눠서 사는 게 좋아요.",
                i.FromHigh <= -20 ? -1 : 0));
        }

        if (s.SpyPe > 0)
            list.Add(new Signal("미국 주식 가격 수준", $"S&P500 PER {s.SpyPe:0.0}배",
                s.SpyPe >= 22 ? "장기 평균(대략 16배 안팎)보다 높아요. 앞으로의 기대수익이 낮아질 수 있어서 지수 비중을 목표 이상으로 늘리지 않는 게 좋아요."
                              : "장기 평균 근처예요.",
                s.SpyPe >= 22 ? 1 : 0));

        if (s.FxHigh > s.FxLow)
        {
            double pos = (s.Fx - s.FxLow) / (s.FxHigh - s.FxLow) * 100;
            list.Add(new Signal("원/달러 환율", $"{s.Fx:N1}원 (1년 범위 {s.FxLow:N0}~{s.FxHigh:N0}원, 아래에서 {pos:0}% 위치)",
                pos <= 30 ? "1년 중 원화가 강한 편이에요. 미국 자산을 원화로 사기에 유리한 환율이에요(환노출 상품)."
                : pos >= 70 ? "1년 중 원화가 약한 편이에요. 미국 자산이 원화로 비싸요. 환헤지(H) 상품이나 나눠 사기를 고려해요."
                : "1년 범위의 중간쯤이에요.",
                0));
        }
        return list;
    }

    // 신호를 모아 이번 달 적립을 어느 쪽에 더 둘지 (목표 비중의 허용 폭 안에서만)
    public static string Summary(List<Signal> sig)
    {
        int tilt = sig.Sum(x => x.Tilt);
        string lean = tilt >= 2 ? "채권 쪽" : tilt <= -2 ? "주식 쪽" : "목표 비중 그대로";
        return $"종합: 이번 달 적립은 {lean}이 무난해 보여요. 단, 목표 비중의 허용 폭(±{Portfolio.Band * 100:0.#}%p) 안에서만 조정하고, " +
               "시장을 맞히려 하기보다 계획대로 꾸준히 사는 게 원칙이에요. (규칙에 따른 참고용 해석이에요)";
    }
}
