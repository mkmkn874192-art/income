namespace KrxScreen;

// 게임 확장팩. 기본 게임(가상 투자 계좌) 위에 원하는 팩만 켠다.
// 새 팩은 IGamePack을 만들어 All에 넣으면 매달 1일 처리와 설정 화면 목록에 붙는다.
public interface IGamePack
{
    string Key { get; }
    string Name { get; }
    string Desc { get; }
    // 매달 1일: 이 팩 때문에 투자 계좌로 들어오는(+) / 나가는(−) 돈을 flow에 더하고, 소식 한 줄을 돌려준다
    string OnMonth(Game.State s, DateTime month, ref double flow);
}

// 재무 항목: 대출·적금·예금
public class FinItem
{
    public string Kind { get; set; }        // 대출 · 적금 · 예금
    public string Name { get; set; }
    public double Balance { get; set; }     // 대출 잔액 / 적금 납입 원금 / 예금 원금 (원)
    public double Rate { get; set; }        // 연 금리 %
    public double Monthly { get; set; }     // 대출 월 상환액(원리금) / 적금 월 납입액 (원)
    public int Months { get; set; }         // 적금·예금 기간 (개월)
    public int Elapsed { get; set; }        // 지난 개월 수
    public double InterestPaid { get; set; } // 대출: 낸 이자 합계
    public bool Done { get; set; }

    public string Line() => Kind switch
    {
        "대출" => $"대출 · {Name} · 잔액 {Won.Krw(Balance)} · 연 {Rate:0.##}% · 매달 {Won.Krw(Monthly)} 상환",
        "적금" => $"적금 · {Name} · 매달 {Won.Krw(Monthly)} · 연 {Rate:0.##}% · {Elapsed}/{Months}개월 · 쌓인 원금 {Won.Krw(Balance)}",
        _ => $"예금 · {Name} · {Won.Krw(Balance)} · 연 {Rate:0.##}% · {Elapsed}/{Months}개월",
    } + (Done ? " · 끝남" : "");
}

public static class GamePacks
{
    public static readonly IGamePack[] All = { new SalaryPack(), new BankPack() };

    public static bool On(string key) => Game.S.Packs.Contains(key);

    // 매달 1일 처리: 켜진 팩마다 돈 흐름을 더해 투자 계좌에 넣는다 (모자라면 현금에서 빠진다)
    public static void RunMonth(DateTime m)
    {
        var s = Game.S;
        double flow = 0;
        var lines = new List<string>();
        if (!On("salary") && s.Monthly > 0) { flow += s.Monthly; lines.Add($"투자금 {Won.Krw(s.Monthly)}"); }
        foreach (var p in All.Where(p => On(p.Key)))
            if (p.OnMonth(s, m, ref flow) is { Length: > 0 } line) lines.Add(line);
        if (lines.Count == 0) return;
        s.Cash += flow;
        s.Deposited += flow;
        Game.Add(m, flow >= 0 ? "월급" : "적자", $"{m:yyyy년 M월} " + string.Join(" · ", lines) + $" → 투자 계좌 {(flow >= 0 ? "+" : "")}{Won.Krw(flow)}" +
                                                  (flow < 0 ? " (모자란 돈은 투자 계좌 현금에서 빠져요)" : ""));
    }

    // 다음 달에 투자 계좌로 들어올 돈 (미리 보기, 실제로 바꾸지 않음)
    public static double PreviewFlow()
    {
        var s = Game.S;
        double flow = On("salary") ? Game.TakeHome(s.Salary) - s.Living : s.Monthly;
        if (On("bank") && On("salary"))
            flow -= s.Items.Where(i => !i.Done && i.Kind != "예금").Sum(i => i.Kind == "대출" ? Math.Min(i.Monthly, i.Balance * (1 + i.Rate / 1200)) : i.Monthly);
        return flow;
    }

    // 예적금 잔액 − 대출 잔액
    public static double BankNet() => !On("bank") ? 0 :
        Game.S.Items.Where(i => !i.Done).Sum(i => i.Kind == "대출" ? -i.Balance : i.Balance);
}

// 연봉·생활비 팩: 월급 실수령에서 생활비를 뺀 돈이 투자 계좌로. 해마다 1월 연봉 인상.
public class SalaryPack : IGamePack
{
    public string Key => "salary";
    public string Name => "연봉·생활비";
    public string Desc => "연봉으로 월 실수령을 계산하고, 생활비를 뺀 돈을 매달 투자해요. 해마다 1월에 연봉이 올라요.";

    public string OnMonth(Game.State s, DateTime m, ref double flow)
    {
        string raise = "";
        if (m.Month == 1 && s.Raise > 0)
        {
            double old = s.Salary;
            s.Salary = Math.Round(s.Salary * (1 + s.Raise) / 1e4) * 1e4;
            raise = $"연봉 인상 {s.Raise * 100:0.#}% ({Won.Krw(old)} → {Won.Krw(s.Salary)}) · ";
        }
        double net = Game.TakeHome(s.Salary);
        flow += net - s.Living;
        return raise + $"월급 실수령 {Won.Krw(net)} − 생활비 {Won.Krw(s.Living)}";
    }
}

// 대출·적금·예금 팩: 매달 이자·상환·납입·만기. 연봉 팩이 켜져 있으면 상환·납입금은 월급에서 나가고,
// 꺼져 있으면 바깥 돈으로 낸다고 보고 잔액만 따라간다. 만기 금액은 투자 계좌로 들어온다.
public class BankPack : IGamePack
{
    public string Key => "bank";
    public string Name => "대출·적금·예금";
    public string Desc => "대출은 매달 이자와 원금을 갚고, 적금은 매달 넣고, 예금은 만기에 이자를 받아요. 만기 금액은 투자 계좌로 들어와요.";
    const double InterestTax = 0.154; // 예적금 이자소득세

    public string OnMonth(Game.State s, DateTime m, ref double flow)
    {
        var parts = new List<string>();
        bool fromSalary = GamePacks.On("salary");
        foreach (var i in s.Items.Where(i => !i.Done))
        {
            switch (i.Kind)
            {
                case "대출":
                {
                    double interest = i.Balance * i.Rate / 1200, pay = Math.Min(i.Monthly, i.Balance + interest);
                    i.Balance = Math.Max(0, i.Balance + interest - pay);
                    i.InterestPaid += Math.Min(interest, pay);
                    if (fromSalary) flow -= pay;
                    parts.Add($"{i.Name} 상환 {Won.Krw(pay)}(이자 {Won.Krw(interest)})");
                    if (pay < interest) Game.Add(m, "경고", $"{i.Name}: 매달 갚는 돈이 이자({Won.Krw(interest)})보다 적어 빚이 늘고 있어요");
                    if (i.Balance < 1) { i.Done = true; Game.Add(m, "업적", $"{i.Name} 대출을 다 갚았어요! 낸 이자 합계 {Won.Krw(i.InterestPaid)}"); }
                    break;
                }
                case "적금":
                {
                    i.Balance += i.Monthly;
                    i.Elapsed++;
                    if (fromSalary) flow -= i.Monthly;
                    parts.Add($"{i.Name} 납입 {Won.Krw(i.Monthly)}");
                    if (i.Elapsed >= i.Months)
                    {
                        // 적금 이자(단리): 매달 넣은 돈이 남은 기간만큼 이자를 받는다
                        double interest = i.Monthly * i.Rate / 1200 * i.Months * (i.Months + 1) / 2 * (1 - InterestTax);
                        Payout(s, m, ref flow, i, i.Balance + interest, interest);
                    }
                    break;
                }
                case "예금":
                {
                    i.Elapsed++;
                    if (i.Elapsed >= i.Months)
                    {
                        double interest = i.Balance * i.Rate / 100 * i.Months / 12 * (1 - InterestTax);
                        Payout(s, m, ref flow, i, i.Balance + interest, interest);
                    }
                    break;
                }
            }
        }
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    static void Payout(Game.State s, DateTime m, ref double flow, FinItem i, double total, double interest)
    {
        i.Done = true;
        flow += total;
        Game.Add(m, "만기", $"{i.Kind} '{i.Name}' 만기 · {Won.Krw(total)} 투자 계좌로 (세후 이자 {Won.Krw(interest)})");
    }
}
