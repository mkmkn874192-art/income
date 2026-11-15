namespace KrxScreen;

// 업종(섹터)별 추세 통계. 값은 업종 안 종목들의 중앙값(%).
public record SectorStat(string Region, string Sector, int N, double Chg1y, double Vs50, double Vs200, double FromHigh, double AvgYield)
{
    // 국면: 단기(50일선)와 중장기(200일선) 위치로 나눈다
    public string Phase => Vs50 > 0 && Vs200 > 0 ? "강세 지속"
                         : Vs50 > 0 ? "반등 시작"
                         : Vs200 > 0 ? "조정 중"
                         : "약세";
}

// 배당 중심 종합 점수 (0~100). 자료가 있는 항목만으로 100점 환산한다.
//   배당 매력 30 · 배당 안정 30 · 배당 주기 5 · 가치 15(국장만) · 성장·추세 20
public static class Score
{
    public static string Region(Quote q) => q.IsUs ? "미장" : "국장";

    static double Median(IEnumerable<double> xs)
    {
        var l = xs.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
    }

    public static List<SectorStat> Sectors(IEnumerable<StockRow> rows) =>
        rows.Where(r => r.Q.Sector != null && r.Q.Chg1y != null && r.Q.Vs50 != null && r.Q.Vs200 != null)
            .GroupBy(r => (Region(r.Q), r.Q.Sector))
            .Where(g => g.Count() >= 5)
            .Select(g => new SectorStat(g.Key.Item1, g.Key.Sector, g.Count(),
                Median(g.Select(r => r.Q.Chg1y.Value)), Median(g.Select(r => r.Q.Vs50.Value)),
                Median(g.Select(r => r.Q.Vs200.Value)), Median(g.Select(r => r.Q.FromHigh ?? 0)),
                g.Where(r => r.Yield > 0).Select(r => r.Yield.Value).DefaultIfEmpty(0).Average()))
            .ToList();

    // 배당률별 점수: 6% 근처가 만점, 8%를 넘으면 '너무 높은 배당'(주가 급락·삭감 위험)으로 깎는다
    static double YieldPoints(double y) =>
        y <= 0 ? 0 : y < 4 ? y / 4 * 25 : y <= 8 ? 25 + (Math.Min(y, 6) - 4) / 2 * 5 : Math.Max(10, 30 - (y - 8) * 2);

    public static void Compute(StockRow r, SectorStat sec, DivInfo d)
    {
        double got = 0, max = 0;
        var notes = new List<string>();
        void Add(string name, double pts, double of) { got += pts; max += of; notes.Add($"{name} {pts:0}/{of:0}"); }

        // 배당 매력
        if (r.Yield is double y) Add("배당 매력", YieldPoints(y), 30);

        // 배당 안정: 배당 기록이 있으면 그걸로, 없으면 국장 가치 분석 결과로
        if (d != null && d.Freq != "없음")
        {
            double s = Math.Min(d.Years, 10) / 10.0 * 15 + (d.Cuts == 0 ? 10 : d.Cuts == 1 ? 5 : 0)
                     + (d.Growth3y > 0 ? 5 : d.Growth3y >= 0 ? 3 : 0) - (d.Special ? 5 : 0);
            Add("배당 안정", Math.Max(0, s), 30);
            Add("배당 주기", d.FreqRank switch { 4 => 5, 3 => 4, 2 => 2, 1 => 1, _ => 0 }, 5);
        }
        else if (r.DivYears != null)
            Add("배당 안정", Math.Min(r.DivYears.Value, 10) / 10.0 * 15 + (r.Labels.Contains("무적자") ? 10 : 3) + 2.5, 30);

        // 가치 (그레이엄, 국장 가치 분석 종목만)
        if (r.Per3 != null || r.Score != null)
            Add("가치", (r.Per3 <= 10 ? 8 : r.Per3 <= 15 ? 6 : 0) + (r.Per3 * r.Pbr <= 22.5 ? 7 : 0), 15);

        // 성장·추세
        if (sec != null && r.Q.Chg1y is double c)
        {
            double rel = c - sec.Chg1y;
            double pts = (rel >= 10 ? 8 : rel >= 0 ? 6 : rel >= -10 ? 3 : 0) + (r.Q.Vs200 > 0 ? 4 : 0) + (sec.Vs50 > 0 ? 4 : 0);
            double of = 16;
            if (r.Growth != null) { pts += r.Growth >= 33 ? 4 : 0; of += 4; }
            Add("성장·추세", pts, of);
        }

        r.Total = max >= 30 ? got / max * 100 : null; // 자료가 너무 적으면 점수를 매기지 않는다
        r.ScoreNote = string.Join(" · ", notes);
    }
}
