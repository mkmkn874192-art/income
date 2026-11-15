namespace KrxScreen;

// 과거 시뮬레이터: 정해진 비중으로 N개월 전에 사서 들고 있었다면 (매달 적립·매년 리밸런싱·배당 재투자 선택)
// 월말 종가 기준, 소수점 수량으로 산다고 가정. 미국 자산은 그달 환율로 원화 환산.
public static class Backtest
{
    public record Asset(string Symbol, string Name, double Weight, string Proxy);
    public record Point(DateTime Month, double Value, double Principal, double DivCash, double Nav);
    public record Result(List<Point> Series, double Final, double Principal, double DivTotal, double Last12Div,
                         double Cagr, double Mdd, double WorstYear, List<string> Notes);

    public static bool IsKrw(string symbol) => symbol.EndsWith(".KS") || symbol.EndsWith(".KQ");

    // 자산과 대용 ETF, 환율의 월별 자료를 받는다 (같은 심볼은 한 번만)
    public static async Task<Dictionary<string, SortedDictionary<DateTime, (double close, double div)>>> Load(IEnumerable<Asset> assets)
    {
        var syms = assets.SelectMany(a => new[] { a.Symbol, a.Proxy }).Where(s => s != null).Append("KRW=X").Distinct().ToList();
        var data = new Dictionary<string, SortedDictionary<DateTime, (double, double)>>();
        foreach (var s in syms) data[s] = await Yahoo.Monthly(s);
        return data;
    }

    // 원화로 바꾼 월별 (종가, 배당). 상장 전 기간은 대용 ETF의 등락으로 거꾸로 이어 붙인다.
    static Dictionary<DateTime, (double close, double div)> Series(Asset a, Dictionary<string, SortedDictionary<DateTime, (double close, double div)>> data,
                                                                   List<DateTime> months, List<string> notes)
    {
        var fx = data["KRW=X"];
        Dictionary<DateTime, (double, double)> Krw(string sym) => data.TryGetValue(sym, out var d)
            ? d.Where(kv => IsKrw(sym) || fx.ContainsKey(kv.Key))
               .ToDictionary(kv => kv.Key, kv => IsKrw(sym) ? kv.Value : (kv.Value.close * fx[kv.Key].close, kv.Value.div * fx[kv.Key].close))
            : new();
        var own = Krw(a.Symbol);
        if (own.Count == 0) { notes.Add($"{a.Name}: 시세 기록이 없어 빼고 계산했어요"); return own; }
        var first = own.Keys.Min();
        if (first <= months[0]) return own;

        if (a.Proxy != null && Krw(a.Proxy) is { Count: > 0 } px && px.ContainsKey(first))
        {
            // first 이전 달을 대용 ETF 등락률로 거꾸로 채운다
            double close = own[first].Item1;
            foreach (var m in months.Where(m => m < first).OrderByDescending(m => m))
            {
                var next = m.AddMonths(1);
                if (!px.TryGetValue(m, out var p) || !px.TryGetValue(next, out var pn) || p.Item1 <= 0) break;
                close = close / (pn.Item1 / p.Item1);
                own[m] = (close, p.Item2 / p.Item1 * close); // 배당도 대용 ETF의 배당률로
            }
            notes.Add($"{a.Name}: {first:yyyy.MM} 상장 전은 {a.Proxy}(원화 환산) 흐름으로 대신했어요");
        }
        else notes.Add($"{a.Name}: {first:yyyy.MM}부터 자료가 있어 그 전엔 현금으로 두었어요");
        return own;
    }

    public static Result Run(List<Asset> assets, Dictionary<string, SortedDictionary<DateTime, (double close, double div)>> data,
                             int months, double start, double monthly, bool rebalance, bool reinvest)
    {
        // 기간: 환율 자료의 마지막 달(진행 중인 달 제외)부터 거꾸로 months개월
        var all = data["KRW=X"].Keys.Where(m => m < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).OrderBy(m => m).ToList();
        var window = all.Skip(Math.Max(0, all.Count - months - 1)).ToList();
        var notes = new List<string>();
        if (window.Count < months + 1) notes.Add($"자료가 {window.Count - 1}개월치만 있어 그만큼만 계산했어요");

        double wsum = assets.Sum(a => a.Weight);
        var w = assets.Select(a => a.Weight / wsum).ToArray();
        var ser = assets.Select(a => Series(a, data, window, notes)).ToArray();
        var tax = assets.Select(a => IsKrw(a.Symbol) ? Income.TaxRate : Income.UsTaxRate).ToArray();
        int n = assets.Count;
        var shares = new double[n];
        double cash = 0, divCash = 0, divTotal = 0, principal = 0, nav = 1, prevValue = 0;
        var points = new List<Point>();
        var monthDivs = new List<double>();

        bool Has(int i, DateTime m) => ser[i].ContainsKey(m) && ser[i][m].close > 0;
        double Value(DateTime m) => Enumerable.Range(0, n).Sum(i => Has(i, m) ? shares[i] * ser[i][m].close : 0) + cash;

        void Buy(DateTime m, double amount, double[] weights)
        {
            for (int i = 0; i < n; i++)
                if (Has(i, m)) shares[i] += amount * weights[i] / ser[i][m].close;
                else cash += amount * weights[i]; // 아직 자료가 없는 자산 몫은 현금으로
        }

        for (int t = 0; t < window.Count; t++)
        {
            var m = window[t];
            double divs = 0;
            if (t == 0) { Buy(m, start, w); principal = start; }
            else
            {
                // 그달 배당 (세후)
                for (int i = 0; i < n; i++)
                    if (Has(i, m) && ser[i][m].div > 0)
                    {
                        double d = shares[i] * ser[i][m].div * (1 - tax[i]);
                        divs += d;
                        if (reinvest) shares[i] += d / ser[i][m].close; else divCash += d;
                    }
                divTotal += divs;
                // 현금으로 두었던 몫은 자료가 생기면 산다
                if (cash > 0) { double c = cash; cash = 0; Buy(m, c, w); }
                double pre = Value(m) + (reinvest ? 0 : divs);
                if (prevValue > 0) nav *= pre / prevValue;
                if (monthly > 0) { Buy(m, monthly, w); principal += monthly; }
                if (rebalance && t % 12 == 0)
                {
                    double total = Value(m);
                    for (int i = 0; i < n; i++) shares[i] = Has(i, m) ? total * w[i] / ser[i][m].close : 0;
                    cash = Enumerable.Range(0, n).Where(i => !Has(i, m)).Sum(i => total * w[i]);
                }
            }
            monthDivs.Add(divs);
            prevValue = Value(m);
            points.Add(new Point(m, prevValue, principal, divCash, nav));
        }

        int len = points.Count - 1;
        double cagr = len > 0 ? Math.Pow(points[^1].Nav, 12.0 / len) - 1 : 0;
        double peak = 0, mdd = 0, worst = double.MaxValue; // worst = 12개월 구간 수익률 중 가장 낮은 값 (플러스일 수도 있다)
        for (int i = 0; i < points.Count; i++)
        {
            peak = Math.Max(peak, points[i].Nav);
            mdd = Math.Min(mdd, points[i].Nav / peak - 1);
            if (i >= 12) worst = Math.Min(worst, points[i].Nav / points[i - 12].Nav - 1);
        }
        if (worst == double.MaxValue) worst = double.NaN;
        return new Result(points, points[^1].Value + points[^1].DivCash, principal, divTotal, monthDivs.TakeLast(12).Sum(),
                          cagr * 100, mdd * 100, worst * 100, notes.Distinct().ToList());
    }

    // 1주 단위로만 살 수 있을 때, 매달 목표 비중을 크게 벗어나지 않고 살 수 있는 최소 월 적립액.
    // 매달 '가장 모자란 자산'부터 1주씩 사는 방식으로 12개월 돌려 본다.
    public static (double amount, List<string> slow) MinMonthly(List<Asset> assets, double[] prices, double band)
    {
        double wsum = assets.Sum(a => a.Weight);
        var w = assets.Select(a => a.Weight / wsum).ToArray();
        for (double m = 100_000; m <= 10_000_000; m += 100_000)
        {
            var qty = new double[assets.Count];
            double cash = 0, maxDev = 0;
            for (int month = 0; month < 12; month++)
            {
                cash += m;
                while (true)
                {
                    double total = Enumerable.Range(0, qty.Length).Sum(i => qty[i] * prices[i]) + cash;
                    int pick = Enumerable.Range(0, qty.Length).Where(i => prices[i] <= cash)
                        .OrderBy(i => total > 0 ? qty[i] * prices[i] / total - w[i] : -w[i]).DefaultIfEmpty(-1).First();
                    if (pick < 0) break;
                    qty[pick]++;
                    cash -= prices[pick];
                }
                double tot = Enumerable.Range(0, qty.Length).Sum(i => qty[i] * prices[i]);
                if (month >= 2 && tot > 0) maxDev = Math.Max(maxDev, Enumerable.Range(0, qty.Length).Max(i => Math.Abs(qty[i] * prices[i] / tot - w[i])));
            }
            if (maxDev <= band / 2 && cash <= m * 0.5)
            {
                var slow = Enumerable.Range(0, assets.Count).Where(i => prices[i] > m * w[i])
                    .Select(i => $"{assets[i].Name}: 1주 {prices[i]:N0}원 > 월 몫 {m * w[i]:N0}원 → 약 {Math.Ceiling(prices[i] / (m * w[i]))}개월에 한 번 1주").ToList();
                return (m, slow);
            }
        }
        return (double.NaN, new());
    }
}
