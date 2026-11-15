using System.Data;

namespace KrxScreen;

// 목표 비중에 맞춘 추천 조합과 기간별 예상치.
// 채권·지수·배당은 국내 상장 ETF(종목코드는 Yahoo에서 이름을 확인한 것만), 개별주는 가치 분석 통과 종목.
public static class Plan
{
    // 분류별 후보. 첫 번째가 기본 추천, 나머지는 대안. Proxy = 같은 지수를 따르는 미국 ETF(배당률을 빌려 씀).
    public record Etf(string Code, string Name, string Why, string Proxy);

    public static readonly Dictionary<string, Etf[]> Etfs = new()
    {
        ["채권"] = new[]
        {
            new Etf("114260", "KODEX 국고채3년", "3년 만기 국고채, 금리 변동에 덜 흔들림", null),
            new Etf("114820", "TIGER 국채3년", "3년 만기 국고채", null),
        },
        ["지수"] = new[]
        {
            new Etf("360750", "TIGER 미국S&P500", "미국 대형주 500개, 환전 없이 국내 계좌로", "VOO"),
            new Etf("379800", "KODEX 미국S&P500", "미국 대형주 500개", "VOO"),
            new Etf("360200", "ACE 미국S&P500", "미국 대형주 500개", "VOO"),
        },
        ["배당"] = new[]
        {
            new Etf("458730", "TIGER 미국배당다우존스", "미국 배당 성장주 100개 (SCHD와 같은 지수)", "SCHD"),
            new Etf("402970", "ACE 미국배당다우존스", "미국 배당 성장주 100개 (SCHD와 같은 지수)", "SCHD"),
        },
    };

    public record Pick(string Cat, string Code, string Name, double Weight, double Price, double? Yield, string Why, string Alternatives);

    // 개별주: 흑자 지속·재무 건전·PER(3년)≤15 통과 → 점수, 배당률 순. 같은 그룹(이름 앞 두 글자)은 하나만, 특별배당 의심 제외.
    public static List<StockRow> Satellites(IEnumerable<StockRow> rows, DataTable screen, Func<string, bool> maybeSpecial, int count)
    {
        if (screen == null) return new();
        var pass = new HashSet<string>();
        bool Has(DataRow r, string c) => screen.Columns.Contains(c) && (r[c] as string) == "O";
        foreach (DataRow r in screen.Rows)
            if (Has(r, "흑자 지속") && Has(r, "재무 건전") && Has(r, "PER(3년)≤15")) pass.Add(r["종목코드"] as string);

        var list = new List<StockRow>();
        foreach (var r in rows.Where(r => pass.Contains(r.Q.Code) && !maybeSpecial(r.Q.Code))
                              .OrderByDescending(r => r.Score ?? 0).ThenByDescending(r => r.Div ?? 0))
        {
            if (list.Any(x => x.Q.Name.Length >= 2 && r.Q.Name.StartsWith(x.Q.Name[..2]))) continue; // 지주사·자회사 중복 피하기
            list.Add(r);
            if (list.Count == count) break;
        }
        return list;
    }

    public record Year(int N, double Principal, double Assets, double Dividend, double MonthlyDividend);

    // 매달 적립하고 목표 비중을 유지한다고 가정한 월 단위 시뮬레이션.
    // yields: 분류별 배당(이자)률, growth: 주식 가격 상승 가정(채권은 0), 배당은 세후로 계산.
    public static List<Year> Project(double start, double monthly, int years, double[] weights, double[] yields,
                                     double stockGrowth, bool reinvest)
    {
        var rows = new List<Year>();
        double assets = start, principal = start;
        for (int y = 1; y <= years; y++)
        {
            double divYear = 0;
            for (int m = 0; m < 12; m++)
            {
                assets += monthly;
                principal += monthly;
                double div = 0, grow = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    double part = assets * weights[i];
                    div += part * yields[i] / 12 * (1 - Income.TaxRate);
                    if (i != 0) grow += part * stockGrowth / 12; // 0 = 채권
                }
                assets += grow + (reinvest ? div : 0);
                divYear += div;
            }
            rows.Add(new Year(y, principal, assets, divYear, divYear / 12));
        }
        return rows;
    }
}
