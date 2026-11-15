namespace KrxScreen;

// 미션·업적: 좋은 재테크 습관을 목표로. 달성하면 소식에 뜨고, 달성한 개수가 레벨이 된다.
public static class GameMissions
{
    // ctx: 지금 가격·포지션을 알려 주는 함수들
    public record Ctx(Func<string, double?> Price, Func<string, string> Pos, double Fx, double SeasonReturn);
    public record Mission(string Key, string Title, string Hint, Func<Game.State, Ctx, bool> Done);

    static double Eval(Game.State s, Ctx c, GameHolding h) => h.Qty * (c.Price(h.Code) ?? h.CostKrw / Math.Max(h.Qty, 1e-9) / Game.Fx(h.Currency, c.Fx)) * Game.Fx(h.Currency, c.Fx);

    static double Share(Game.State s, Ctx c, string pos)
    {
        double total = s.Holdings.Sum(h => Eval(s, c, h));
        return total <= 0 ? 0 : s.Holdings.Where(h => c.Pos(h.Code) == pos).Sum(h => Eval(s, c, h)) / total;
    }

    public static readonly Mission[] All =
    {
        new("first_buy", "첫 선수 영입", "아무 종목이나 처음 사 보기", (s, c) => s.Holdings.Count > 0 || s.News.Any(n => n.Kind == "매매")),
        new("first_div", "첫 배당", "배당을 한 번 받기 (배당락일 전에 사 두기)", (s, c) => s.DivReceived > 0),
        new("diversify5", "분산 투자", "5종목 이상 나눠 담기", (s, c) => s.Holdings.Count >= 5),
        new("defense20", "튼튼한 수비", "수비수(채권·금)를 20% 이상 두기", (s, c) => s.Holdings.Count >= 2 && Share(s, c, "수비수") >= 0.2),
        new("hold30", "흔들리지 않기", "게임 시작 후 30일 동안 한 번도 팔지 않기",
            (s, c) => (DateTime.Now - s.StartedAt).TotalDays >= 30 && s.Holdings.Count > 0 && !s.News.Any(n => n.Kind == "매매" && n.Text.Contains("매도"))),
        new("div_month10", "월 배당 10만원", "올해 받은 배당·이자(세후)가 120만원 넘기", (s, c) => s.FinIncome.GetValueOrDefault(DateTime.Now.Year) * (1 - KrTax.DividendTax) >= 1_200_000),
        new("emergency", "비상금 3개월", "예적금에 생활비 3개월치 모으기 (대출·적금·예금 팩)",
            (s, c) => GamePacks.On("bank") && s.Living > 0 && s.Items.Where(i => !i.Done && i.Kind != "대출").Sum(i => i.Balance) >= s.Living * 3),
        new("debt_free", "빚 졸업", "대출을 하나 다 갚기", (s, c) => s.Items.Any(i => i.Kind == "대출" && i.Done)),
        new("grade_a", "A등급 시즌", "시즌 수익률 5% 이상 (지금 시즌 기준)", (s, c) => c.SeasonReturn >= 5),
        new("plus10", "자산 +10%", "넣은 돈보다 총자산 10% 이상 불리기",
            (s, c) => s.Deposited > 0 && (s.Cash + s.Holdings.Sum(h => Eval(s, c, h))) >= s.Deposited * 1.1),
    };

    // 새로 달성한 미션을 기록하고 소식으로 알린다
    public static void Check(Game.State s, Ctx c)
    {
        foreach (var m in All)
        {
            if (s.Missions.Contains(m.Key)) continue;
            bool done;
            try { done = m.Done(s, c); } catch { done = false; } // 계산 중 문제가 나도 게임은 계속
            if (!done) continue;
            s.Missions.Add(m.Key);
            Game.Add(DateTime.Now, "업적", $"미션 달성: {m.Title} — {m.Hint} · 레벨 {Level(s)}");
        }
    }

    public static int Level(Game.State s) => s.Missions.Count;
}
