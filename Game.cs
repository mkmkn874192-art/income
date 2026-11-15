using System.Text.Json;

namespace KrxScreen;

// 게임 모드: 실제 시세·실제 시간으로 굴리는 가상 계좌. 실제 주문(키움)과는 전혀 연결되지 않는다.
// 앱을 꺼 둔 동안에도 시간은 흐른다 — 다시 켜면 그사이 월급·배당·예약 주문·소식을 한 번에 처리한다.
public class GameHolding
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Market { get; set; }   // KOSPI·KOSDAQ·US·US ETF
    public string Currency { get; set; } = "KRW";
    public bool Etf { get; set; }
    public double Qty { get; set; }
    public double CostKrw { get; set; }  // 산 금액 합계 (수수료 포함, 원)
    public DateTime Since { get; set; }  // 처음 산 때 (이전 배당은 받지 않는다)
}

public class GameOrder
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Market { get; set; }
    public string Currency { get; set; } = "KRW";
    public bool Etf { get; set; }
    public bool Buy { get; set; }
    public double Qty { get; set; }
    public DateTime Placed { get; set; }
}

public record GameNews(DateTime At, string Kind, string Text);

public static class Game
{
    // 비용 (대략값 — 증권사·시기마다 다르다)
    public const double KrFee = 0.00015;     // 국내 매매 수수료 0.015%
    public const double KrSellTax = 0.0020;  // 국내 주식 팔 때 거래세 약 0.2% (ETF는 없음)
    public const double UsFee = 0.0025;      // 미국 매매 수수료 0.25%
    public const double UsFx = 0.001;        // 환전 비용 0.1% (환율 우대 90% 기준)

    public class State
    {
        public bool Started { get; set; }
        public double Seed { get; set; }
        public double Monthly { get; set; }       // 연봉 팩이 꺼져 있을 때 매달 1일 투자 계좌로 넣는 돈 (원)
        public List<string> Packs { get; set; } = new(); // 켜진 확장팩 (GamePacks.All의 Key)
        public List<FinItem> Items { get; set; } = new(); // 대출·적금·예금
        public double Salary { get; set; }        // 연봉 (세전, 원)
        public double Living { get; set; }        // 월 생활비 (원)
        public double Raise { get; set; }         // 해마다 1월 연봉 인상률 (0.03 = 3%)
        public double Cash { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastTick { get; set; }
        public List<GameHolding> Holdings { get; set; } = new();
        public List<GameOrder> Pending { get; set; } = new();
        public List<GameNews> News { get; set; } = new();
        public Dictionary<string, double> LastPrice { get; set; } = new(); // 종목별 지난번 가격 (급등락 소식용)
        public double LastFx { get; set; }
        public double DivReceived { get; set; }   // 받은 배당 합계 (세후, 원)
        public double FeesPaid { get; set; }      // 수수료·세금·환전 비용 합계
        public double Deposited { get; set; }     // 시드 + 받은 월급 합계 (수익률 기준)
        public string Season { get; set; }        // "2026년 4분기"
        public double SeasonStartValue { get; set; }
        public double SeasonStartDeposited { get; set; }
    }

    static readonly string File_ = Path.Combine(Market.Dir, "game.json");
    static State s;
    public static State S => s ??= Load();

    static State Load()
    {
        try { return File.Exists(File_) ? JsonSerializer.Deserialize<State>(File.ReadAllText(File_)) ?? new() : new(); }
        catch { return new(); } // 깨졌으면 새 게임
    }

    public static void Save()
    {
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(S));
    }

    // ---------- 시계 ----------

    static readonly TimeZoneInfo Kst = Find("Korea Standard Time"), Et = Find("Eastern Standard Time");
    static TimeZoneInfo Find(string id) { try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { return TimeZoneInfo.Local; } }

    // 공휴일은 아직 빼지 않는다 (평일 장 시간만 본다)
    public static bool Open(string currency, DateTime nowLocal)
    {
        var utc = nowLocal.ToUniversalTime();
        if (currency == "USD")
        {
            var t = TimeZoneInfo.ConvertTimeFromUtc(utc, Et);
            return t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && t.TimeOfDay >= new TimeSpan(9, 30, 0) && t.TimeOfDay < new TimeSpan(16, 0, 0);
        }
        var k = TimeZoneInfo.ConvertTimeFromUtc(utc, Kst);
        return k.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && k.TimeOfDay >= new TimeSpan(9, 0, 0) && k.TimeOfDay < new TimeSpan(15, 30, 0);
    }

    // 다음 개장 시각 (내 PC 시간대로)
    public static DateTime NextOpen(string currency, DateTime nowLocal)
    {
        var tz = currency == "USD" ? Et : Kst;
        var open = currency == "USD" ? new TimeSpan(9, 30, 0) : new TimeSpan(9, 0, 0);
        var t = TimeZoneInfo.ConvertTimeFromUtc(nowLocal.ToUniversalTime(), tz);
        for (int i = 0; i < 8; i++)
        {
            var d = t.Date.AddDays(i) + open;
            if (d <= t || d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), tz).ToLocalTime();
        }
        return nowLocal;
    }

    public static string SeasonOf(DateTime d) => $"{d.Year}년 {(d.Month - 1) / 3 + 1}분기";

    // ---------- 계좌 ----------

    public record Finance(double Monthly, List<string> Packs, double Salary, double Living, double Raise, List<FinItem> Items);

    public static void Start(double seed, Finance f, double fx)
    {
        var now = DateTime.Now;
        s = new State
        {
            Started = true, Seed = seed, Cash = seed, StartedAt = now, LastTick = now, LastFx = fx,
            Deposited = seed, Season = SeasonOf(now), SeasonStartValue = seed, SeasonStartDeposited = seed,
        };
        SetFinance(f, quiet: true);
        Add(now, "시스템", $"새 게임 시작 · 시드머니 {Won.Krw(seed)} · {FinanceLine()}");
        Save();
    }

    // 게임 중에도 재무 설정을 바꿀 수 있다 (다음 달 1일부터 적용)
    public static void SetFinance(Finance f, bool quiet = false)
    {
        S.Packs = f.Packs.ToList(); S.Salary = f.Salary; S.Living = f.Living; S.Raise = f.Raise; S.Monthly = f.Monthly;
        S.Items = f.Items.ToList();
        if (!quiet) { Add(DateTime.Now, "시스템", "재무 설정 변경 · " + FinanceLine()); Save(); }
    }

    public static string FinanceLine()
    {
        var parts = new List<string>();
        if (GamePacks.On("salary")) parts.Add($"연봉 {Won.Krw(S.Salary)} (월 실수령 약 {Won.Krw(TakeHome(S.Salary))}) · 생활비 {Won.Krw(S.Living)}");
        else if (S.Monthly > 0) parts.Add($"매달 {Won.Krw(S.Monthly)} 투자");
        if (GamePacks.On("bank") && S.Items.Count > 0) parts.Add($"대출·예적금 {S.Items.Count(i => !i.Done)}건");
        double flow = GamePacks.PreviewFlow();
        parts.Add($"매달 투자 계좌로 {(flow >= 0 ? "" : "−")}{Won.Krw(Math.Abs(flow))}");
        return string.Join(" · ", parts);
    }

    // 연봉(세전) → 월 실수령 대략값. 4대 보험·소득세를 연봉 구간별 실수령 비율로 근사 (부양가족 1명, 비과세 없음 기준)
    static readonly (double salary, double ratio)[] NetRatio =
    {
        (0, 0.93), (20e6, 0.92), (30e6, 0.90), (40e6, 0.88), (50e6, 0.86), (60e6, 0.84), (80e6, 0.81), (100e6, 0.78), (150e6, 0.735), (200e6, 0.70),
    };
    public static double TakeHome(double salary)
    {
        if (salary <= 0) return 0;
        double ratio = NetRatio[^1].ratio;
        for (int i = 1; i < NetRatio.Length; i++)
            if (salary <= NetRatio[i].salary)
            {
                var (s0, r0) = NetRatio[i - 1]; var (s1, r1) = NetRatio[i];
                ratio = r0 + (r1 - r0) * (salary - s0) / (s1 - s0);
                break;
            }
        return Math.Round(salary * ratio / 12 / 1000) * 1000;
    }

    public static void Add(DateTime at, string kind, string text)
    {
        S.News.Add(new GameNews(at, kind, text));
        if (S.News.Count > 500) S.News.RemoveRange(0, S.News.Count - 500);
    }

    public static double Fx(string currency, double fx) => currency == "USD" ? fx : 1;

    // 총자산 (원). price: 종목코드 → 지금 가격(그 나라 통화), 없으면 null
    public static double Value(Func<string, double?> price, double fx) =>
        S.Cash + S.Holdings.Sum(h => h.Qty * (price(h.Code) ?? h.CostKrw / Math.Max(h.Qty, 1e-9) / Fx(h.Currency, fx)) * Fx(h.Currency, fx));

    // 주문: 장이 열려 있으면 바로 체결, 닫혀 있으면 다음 장에 체결되게 예약. 결과 문구를 돌려준다.
    public static string Place(GameOrder o, double? price, double fx)
    {
        var now = DateTime.Now;
        if (o.Qty <= 0) return "수량을 넣어 주세요.";
        if (!o.Buy && (S.Holdings.FirstOrDefault(h => h.Code == o.Code)?.Qty ?? 0) < o.Qty) return "가진 수량보다 많이 팔 수 없어요.";
        if (!Open(o.Currency, now) || price is not > 0)
        {
            o.Placed = now;
            S.Pending.Add(o);
            Add(now, "예약", $"{o.Name} {o.Qty:N0}주 {(o.Buy ? "매수" : "매도")} 예약 · {NextOpen(o.Currency, now):M/d HH:mm} 장이 열리면 그때 가격으로 체결돼요");
            Save();
            return $"장이 닫혀 있어 예약했어요. {NextOpen(o.Currency, now):M/d HH:mm}에 장이 열리면 체결돼요.";
        }
        string r = Execute(o, price.Value, fx, now);
        Save();
        return r;
    }

    static string Execute(GameOrder o, double price, double fx, DateTime at)
    {
        double f = Fx(o.Currency, fx), amount = o.Qty * price * f;
        double cost = amount * (o.Currency == "USD" ? UsFee + UsFx : KrFee) + (!o.Buy && o.Currency != "USD" && !o.Etf ? amount * KrSellTax : 0);
        if (o.Buy)
        {
            if (S.Cash < amount + cost) { Add(at, "실패", $"{o.Name} {o.Qty:N0}주 매수 실패 · 현금이 {Won.Krw(amount + cost - S.Cash)} 모자라요"); return "현금이 모자라요."; }
            S.Cash -= amount + cost;
            var h = S.Holdings.FirstOrDefault(x => x.Code == o.Code);
            if (h == null) S.Holdings.Add(h = new GameHolding { Code = o.Code, Name = o.Name, Market = o.Market, Currency = o.Currency, Etf = o.Etf, Since = at });
            h.Qty += o.Qty;
            h.CostKrw += amount + cost;
        }
        else
        {
            var h = S.Holdings.FirstOrDefault(x => x.Code == o.Code);
            if (h == null || h.Qty < o.Qty) { Add(at, "실패", $"{o.Name} 매도 실패 · 가진 수량이 모자라요"); return "가진 수량이 모자라요."; }
            double costPart = h.CostKrw * o.Qty / h.Qty, gain = amount - cost - costPart;
            S.Cash += amount - cost;
            h.CostKrw -= costPart;
            h.Qty -= o.Qty;
            if (h.Qty <= 1e-9) S.Holdings.Remove(h);
            Add(at, "매매", $"{o.Name} {o.Qty:N0}주 매도 · {Won.Price(price, o.Currency)} · 손익 {(gain >= 0 ? "+" : "")}{Won.Krw(gain)}");
        }
        S.FeesPaid += cost;
        S.LastPrice[o.Code] = price;
        if (o.Buy) Add(at, "매매", $"{o.Name} {o.Qty:N0}주 매수 · {Won.Price(price, o.Currency)} · {Won.Krw(amount + cost)} (비용 {Won.Krw(cost)})");
        return $"{o.Name} {o.Qty:N0}주 {(o.Buy ? "매수" : "매도")} 체결 · {Won.Price(price, o.Currency)}";
    }

    // ---------- 시간 흐름 ----------

    // 지난 처리 이후 지금까지 일어난 일을 처리하고, 새로 생긴 소식을 돌려준다.
    // price: 지금 가격, divs: 종목코드 → 배당 기록(배당락일, 주당 금액)
    public static List<GameNews> Tick(Func<string, double?> price, double fx, IReadOnlyDictionary<string, List<Yahoo.Point>> divs)
    {
        if (!S.Started) return new();
        var now = DateTime.Now;
        int before = S.News.Count;
        var from = S.LastTick;

        // 1) 매달 1일: 켜진 확장팩(연봉·생활비, 대출·적금·예금)과 매달 투자금
        for (var m = new DateTime(from.Year, from.Month, 1).AddMonths(1); m <= now; m = m.AddMonths(1))
            GamePacks.RunMonth(m);

        // 2) 배당: 배당락일이 지난번~지금 사이이고, 그 전에 산 종목만 (세후)
        foreach (var h in S.Holdings)
            if (divs.TryGetValue(h.Code, out var pays))
                foreach (var p in pays.Where(p => p.Date > from.Date && p.Date <= now && p.Date > h.Since.Date))
                {
                    double got = h.Qty * p.Close * Fx(h.Currency, fx) * (1 - Income.Tax(h.Currency));
                    S.Cash += got;
                    S.DivReceived += got;
                    Add(p.Date, "배당", $"{h.Name} 배당 {Won.Krw(got)} (주당 {Won.Price(p.Close, h.Currency)} × {h.Qty:N0}주, 세후)");
                }

        // 3) 예약 주문: 장이 열려 있으면 지금 가격으로
        foreach (var o in S.Pending.ToList())
            if (Open(o.Currency, now) && price(o.Code) is double px && px > 0)
            {
                S.Pending.Remove(o);
                Execute(o, px, fx, now);
            }

        // 4) 소식: 보유 종목 급등락(±5%), 환율 2% 이상 변동
        foreach (var h in S.Holdings)
            if (price(h.Code) is double px && px > 0)
            {
                if (S.LastPrice.TryGetValue(h.Code, out var last) && last > 0)
                {
                    double ch = (px / last - 1) * 100;
                    if (Math.Abs(ch) >= 5) Add(now, "소식", $"{h.Name} {(ch > 0 ? "급등" : "급락")} {ch:+0.0;-0.0}% · {Won.Price(last, h.Currency)} → {Won.Price(px, h.Currency)}");
                }
                S.LastPrice[h.Code] = px;
            }
        if (S.LastFx > 0 && Math.Abs(fx / S.LastFx - 1) >= 0.02)
            Add(now, "소식", $"환율 {(fx > S.LastFx ? "상승" : "하락")} · 1달러 {S.LastFx:N0}원 → {fx:N0}원 (미국 종목 원화 가치가 {(fx > S.LastFx ? "올라요" : "내려요")})");
        if (S.LastFx <= 0 || Math.Abs(fx / S.LastFx - 1) >= 0.02) S.LastFx = fx;

        // 5) 시즌(분기) 결산
        string season = SeasonOf(now);
        if (S.Season != season)
        {
            double v = Value(price, fx), added = S.Deposited - S.SeasonStartDeposited;
            double ret = S.SeasonStartValue > 0 ? (v - added - S.SeasonStartValue) / S.SeasonStartValue * 100 : 0;
            Add(now, "시즌", $"{S.Season} 결산 · 총자산 {Won.Krw(v)} · 수익률 {ret:+0.0;-0.0}% (넣은 돈 제외) · 등급 {Grade(ret)}");
            S.Season = season;
            S.SeasonStartValue = v;
            S.SeasonStartDeposited = S.Deposited;
        }

        S.LastTick = now;
        Save();
        return S.News.Skip(before).OrderBy(n => n.At).ToList();
    }

    public static string Grade(double seasonReturn) => seasonReturn switch
    {
        >= 10 => "S", >= 5 => "A", >= 1 => "B", >= -3 => "C", >= -10 => "D", _ => "F",
    };
}
