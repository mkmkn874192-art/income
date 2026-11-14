using System.Data;

namespace KrxScreen;

// 시장 화면 한 줄 = 시세 + (있으면) 스크리닝 결과 + 라벨
public class StockRow
{
    public Quote Q;
    public double? Div, Per, Pbr, Roe, Growth, DebtRatio;
    public int? Score, DivYears;
    public readonly List<string> Labels = new();
    public double Rank; // 시장 내 시총 순위
}

// 라벨 부여와 검색어 해석
public static class Labels
{
    // 라벨 -> 검색어 동의어, 정렬 기준(값이 클수록 위가 기본, Asc면 작을수록 위)
    public record Rule(string Label, string[] Words, Func<StockRow, double?> Key, bool Asc = false, bool NeedsScreen = false);

    public static readonly Rule[] Rules =
    {
        new("고배당", new[] { "배당주", "배당", "고배당", "고배당주", "배당높은", "배당많은" }, r => r.Div, NeedsScreen: true),
        new("초고배당", new[] { "초고배당" }, r => r.Div, NeedsScreen: true),
        new("꾸준배당", new[] { "꾸준배당", "배당귀족", "연속배당", "장기배당" }, r => r.DivYears, NeedsScreen: true),
        new("가치주", new[] { "가치주", "가치", "저평가", "저평가주" }, r => r.Per * r.Pbr, Asc: true, NeedsScreen: true),
        new("저PER", new[] { "저per", "per낮은" }, r => r.Per, Asc: true, NeedsScreen: true),
        new("저PBR", new[] { "저pbr", "pbr낮은", "자산주" }, r => r.Pbr, Asc: true, NeedsScreen: true),
        new("고ROE", new[] { "고roe", "roe", "roe높은", "수익성" }, r => r.Roe, NeedsScreen: true),
        new("성장주", new[] { "성장주", "성장", "이익성장" }, r => r.Growth, NeedsScreen: true),
        new("재무건전", new[] { "재무건전", "안정", "안정적", "안전" }, r => r.DebtRatio, Asc: true, NeedsScreen: true),
        new("무적자", new[] { "무적자", "흑자", "꾸준흑자" }, r => r.Roe, NeedsScreen: true),
        new("그레이엄 우수", new[] { "그레이엄", "그레이엄우수", "고득점", "점수높은" }, r => r.Score, NeedsScreen: true),
        new("대형주", new[] { "대형주", "대형", "우량주", "블루칩", "시총상위" }, r => r.Q.Cap),
        new("중형주", new[] { "중형주", "중형" }, r => r.Q.Cap),
        new("소형주", new[] { "소형주", "소형", "중소형" }, r => r.Q.Cap),
        new("급등", new[] { "급등", "급등주", "상승", "오른" }, r => r.Q.ChangeRate),
        new("급락", new[] { "급락", "급락주", "하락", "내린" }, r => r.Q.ChangeRate, Asc: true),
        new("거래대금 상위", new[] { "거래대금", "거래대금상위", "거래많은", "인기", "핫한" }, r => r.Q.TradeValue),
        new("우선주", new[] { "우선주" }, r => r.Q.Cap),
        new("코스피", new[] { "코스피", "kospi", "유가증권" }, r => r.Q.Cap),
        new("코스닥", new[] { "코스닥", "kosdaq" }, r => r.Q.Cap),
        new("미국", new[] { "미국", "미장", "us", "미국주식", "해외", "해외주식" }, r => r.Q.Cap),
        new("ETF", new[] { "etf", "이티에프" }, r => r.Q.Cap),
    };

    // 칩으로 바로 누를 라벨
    public static readonly string[] Chips = { "고배당", "꾸준배당", "가치주", "저PBR", "고ROE", "성장주", "대형주", "급등", "급락", "거래대금 상위" };

    static string Norm(string s) => s.Replace(" ", "").ToLowerInvariant();

    public static Rule Find(string token)
    {
        string t = Norm(token);
        return Rules.FirstOrDefault(r => Norm(r.Label) == t || r.Words.Contains(t));
    }

    public static List<StockRow> Build(List<Quote> quotes, DataTable screen)
    {
        var rows = quotes.Select(q => new StockRow { Q = q }).ToList();
        var byCode = rows.ToDictionary(r => r.Q.Code);

        if (screen != null)
            foreach (DataRow s in screen.Rows)
            {
                if (!byCode.TryGetValue(s["종목코드"] as string ?? "", out var r)) continue;
                double? D(string c) => s[c] is double v ? v : null;
                r.Div = D("배당수익률(%)"); r.Per = D("PER"); r.Pbr = D("PBR"); r.Roe = D("ROE(%)");
                r.Growth = D("이익성장(%)"); r.DebtRatio = D("부채비율(%)");
                r.Score = s["점수"] as int?; r.DivYears = s["배당연수"] as int?;
                bool O(string c) => (s[c] as string) == "O";
                if (r.Div >= 3) r.Labels.Add("고배당");
                if (r.Div >= 5) r.Labels.Add("초고배당");
                if (r.DivYears >= 10) r.Labels.Add("꾸준배당");
                if (O("PER×PBR≤22.5")) r.Labels.Add("가치주");
                if (r.Per is > 0 and <= 10) r.Labels.Add("저PER");
                if (r.Pbr is > 0 and < 1) r.Labels.Add("저PBR");
                if (r.Roe >= 15) r.Labels.Add("고ROE");
                if (O("이익 성장")) r.Labels.Add("성장주");
                if (O("재무 건전")) r.Labels.Add("재무건전");
                if (O("무적자")) r.Labels.Add("무적자");
                if (r.Score >= 7) r.Labels.Add("그레이엄 우수");
            }

        foreach (var g in rows.GroupBy(r => r.Q.Market))
        {
            int i = 0;
            foreach (var r in g.OrderByDescending(r => r.Q.Cap))
            {
                r.Rank = ++i;
                r.Labels.Add(i <= 100 ? "대형주" : i <= 300 ? "중형주" : "소형주");
            }
        }
        foreach (var r in rows.OrderByDescending(r => r.Q.TradeValue).Take(50)) r.Labels.Add("거래대금 상위");
        foreach (var r in rows)
        {
            if (r.Q.ChangeRate >= 5) r.Labels.Add("급등");
            if (r.Q.ChangeRate <= -5) r.Labels.Add("급락");
            if (r.Q.IsUs)
            {
                // 미국은 Yahoo 배당률로 배당 라벨만 붙인다 (재무 분석은 국내만)
                r.Div = r.Q.DivYield;
                if (r.Div >= 3) r.Labels.Add("고배당");
                if (r.Div >= 5) r.Labels.Add("초고배당");
                r.Labels.Add("미국");
                if (r.Q.Market == "US ETF") r.Labels.Add("ETF");
            }
            else
            {
                if (!r.Q.Code.EndsWith("0")) r.Labels.Add("우선주");
                r.Labels.Add(r.Q.Market == "KOSPI" ? "코스피" : "코스닥");
            }
        }
        return rows;
    }

    // 검색: 공백으로 나눈 단어를 모두 만족(AND). 단어가 라벨이면 라벨 필터, 숫자면 코드 앞자리, 아니면 종목명·라벨 포함.
    // 정렬은 마지막으로 나온 라벨의 기준, 없으면 시총 순.
    public static List<StockRow> Search(IEnumerable<StockRow> rows, string query, out Rule sortRule)
    {
        sortRule = null;
        var q = rows;
        foreach (var token in (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rule = Find(token);
            if (rule != null)
            {
                q = q.Where(r => r.Labels.Contains(rule.Label));
                sortRule = rule;
            }
            else if (token.All(char.IsDigit))
                q = q.Where(r => r.Q.Code.StartsWith(token));
            else
            {
                string t = Norm(token);
                q = q.Where(r => Norm(r.Q.Name).Contains(t) || r.Q.Code.Contains(token.ToUpperInvariant())
                                 || r.Labels.Any(l => Norm(l).Contains(t)));
            }
        }
        if (sortRule == null) return q.OrderByDescending(r => r.Q.Cap).ToList();
        var key = sortRule.Key;
        var withKey = q.Where(r => key(r) != null);
        var sorted = sortRule.Asc ? withKey.OrderBy(r => key(r)) : withKey.OrderByDescending(r => key(r));
        return sorted.Concat(q.Where(r => key(r) == null)).ToList();
    }
}
