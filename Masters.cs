namespace KrxScreen;

// 투자 대가 따라하기: 널리 알려진 원칙을 이 앱의 자료(국내 상장 ETF + 가치 분석 결과)로 단순하게 옮긴 것.
// 대가가 실제로 이렇게 투자했다는 뜻이 아니라 '공개된 원칙을 앱이 해석한 구성'이다.
public static class Masters
{
    public record Holding(string Code, string Name, double Weight, string Proxy, bool Bond, string Why);
    public record Style(string Key, string Who, string Name, string Line, string Idea, string Caution, bool Picks)
    {
        public override string ToString() => Who; // 선택 상자에는 이름만
    }

    // 국내 상장 ETF (코드·이름은 Yahoo에서 확인한 것). Proxy = 상장 전 기간을 대신할 미국 ETF.
    static Holding Etf(string code, double w, string why) => code switch
    {
        "114260" => new(code, "KODEX 국고채3년", w, null, true, why),
        "148070" => new(code, "KIWOOM 국고채10년", w, null, true, why),
        "360750" => new(code, "TIGER 미국S&P500", w, "VOO", false, why),
        "133690" => new(code, "TIGER 미국나스닥100", w, "QQQ", false, why),
        "069500" => new(code, "KODEX 200", w, null, false, why),
        "458730" => new(code, "TIGER 미국배당다우존스", w, "SCHD", false, why),
        "411060" => new(code, "ACE KRX금현물", w, "GLD", false, why),
        _ => throw new ArgumentException(code),
    };

    public static readonly Style[] All =
    {
        new("income", MainForm.AppName + " 기본", "배당으로 매달 들어오는 돈",
            "채권 50 · 미국 지수 20 · 배당 ETF 20 · 개별주 10",
            "흔들림을 줄이는 채권을 절반 두고, 미국 대표 지수와 배당 ETF로 꾸준히 배당을 받아요. 개별주는 앱의 가치 분석을 통과한 종목 몇 개만 조금 담아요.",
            "배당에 무게를 둬서 크게 오르는 장에서는 주식만 산 사람보다 덜 벌어요.", false),
        new("buffett", "워런 버핏", "평범한 사람에게 권한 방법",
            "미국 S&P500 90 · 단기 국채 10",
            "버핏은 2013년 주주 서한에서 자기가 세상을 떠난 뒤 아내 몫의 돈을 'S&P500 인덱스 펀드 90%, 단기 국채 10%'로 두라고 적었어요. 종목을 고르는 대신 미국 대표 기업 500개를 싸게 오래 들고 가는 방법이에요.",
            "주식이 90%라 하락장에서는 크게 흔들려요(과거 미국 주식은 고점에서 -30~50%까지 떨어진 적이 있어요). 10년 이상 버틸 수 있는 돈에 맞아요.", false),
        new("munger", "찰리 멍거", "훌륭한 회사 몇 개에 집중",
            "돈 잘 버는 우량주 5개 80 · 채권 20",
            "멍거는 '좋은 회사를 적당한 값에 사서 오래 들고 가라', '분산은 모르는 사람을 위한 것'이라고 했어요. 앱은 적자 없이 자본 대비 이익(ROE)이 15% 넘고 빚이 적은 회사 중, 이익 대비 너무 비싸지 않은(3년 평균 PER 25배 이하) 회사를 5개 골라요.",
            "종목이 적어 한 회사의 나쁜 소식에 크게 흔들려요. 원래 방식엔 채권이 없지만 앱은 기본 안전판으로 20%를 뒀어요. 초보자에겐 권하지 않아요.", true),
        new("graham", "벤저민 그레이엄", "싸고 튼튼한 회사 + 채권 반반",
            "기준 통과 종목 10개 50 · 채권 50",
            "『현명한 투자자』에서 '방어적 투자자'에게 주식과 채권을 반반 두고, 이익이 꾸준하고 재무가 튼튼하며 싼(3년 평균 PER 15배 이하, PER×PBR 22.5 이하) 회사를 10~30개 나눠 담으라고 했어요.",
            "싼 회사는 싼 이유가 있기도 해요(가치 함정). 오르는 장에서 성장주보다 뒤처질 수 있어요.", true),
        new("lynch", "피터 린치", "이익이 크는데 아직 싼 회사",
            "성장 대비 싼 종목 8개 80 · 채권 20",
            "린치는 '이익이 늘어나는 속도에 비해 주가가 싼 회사'를 찾았어요. PER을 이익 성장률로 나눈 값(PEG)이 1 이하면 싸다고 봤어요. 앱은 재무 자료의 이익 성장을 1년 평균으로 바꿔 PEG를 계산하고, 빚이 적은 회사 중 PEG가 낮은 순으로 8개를 골라요.",
            "과거 이익 성장이 앞으로도 이어진다는 보장이 없어요. 린치는 회사를 직접 공부하라고 강조했어요.", true),
        new("bogle", "존 보글", "시장 전체를 싸게, 나이만큼 채권",
            "채권 = 나이% · 나머지는 미국·한국 지수",
            "인덱스 펀드를 만든 보글은 '건초 더미에서 바늘을 찾지 말고 건초 더미를 통째로 사라'고 했어요. 채권 비중은 대략 나이만큼(40세면 40%) 두는 방법을 권했어요.",
            "시장 평균만큼만 벌어요. 대신 비용이 낮고 고를 게 없어 오래 지키기 쉬워요.", false),
        new("dalio", "레이 달리오", "어떤 경제 상황에도 버티기 (올웨더)",
            "미국 주식 30 · 장기 국채 40 · 중기 국채 15 · 금 15",
            "달리오는 경기가 좋을 때·나쁠 때·물가가 오를 때·내릴 때 각각 강한 자산을 섞어, 어떤 상황에서도 크게 무너지지 않게 하는 '올웨더'를 소개했어요. 원래는 원자재 7.5%와 금 7.5%인데 앱은 둘을 금 15%로 합쳤어요.",
            "덜 흔들리는 대신 주식이 크게 오르는 장에서는 많이 뒤처져요. 금리가 빠르게 오를 때는 채권과 주식이 같이 떨어질 수 있어요.", false),
    };

    // 비교용 기준선 (한 상품에 전부)
    public static readonly (string Name, Holding[] List)[] References =
    {
        ("공격형 · 나스닥100", new[] { Etf("133690", 1, "") }),
        ("공격형 · 코스피200", new[] { Etf("069500", 1, "") }),
        ("방어형 · 국고채", new[] { Etf("114260", 1, "") }),
    };

    // 처음이라 고르기 어려운 사람용: 투자 기간, 하락을 견디는 정도, 원하는 것 → 방식 하나
    // horizon 0=3년 미만 1=3~10년 2=10년 이상, drop 0=팔겠다 1=버틴다 2=더 산다, goal 0=배당 1=둘 다 2=자산 키우기
    public static (Style style, string reason) Recommend(int horizon, int drop, int goal, int age)
    {
        int risk = horizon + drop;
        string key = goal == 0 ? "income"
                   : horizon == 0 || risk <= 1 ? "dalio"
                   : risk >= 3 && goal == 2 ? "buffett"
                   : "bogle";
        var s = All.First(x => x.Key == key);
        string Horizon = new[] { "3년 안에 쓸 수도 있는 돈", "3~10년 둘 돈", "10년 넘게 둘 돈" }[horizon];
        string Drop = new[] { "20% 떨어지면 팔고 싶은 편", "불안해도 버티는 편", "오히려 더 사는 편" }[drop];
        string Goal = new[] { "매달 들어오는 배당", "배당과 자산 키우기 둘 다", "자산 키우기" }[goal];
        var reason = $"{Horizon}이고, {Drop}이고, {Goal}을 원한다고 하셨어요. → {s.Who} 방식이 맞아 보여요.\n";
        reason += key switch
        {
            "income" => "채권이 절반이라 덜 흔들리고, 배당이 매달·분기마다 들어와요.",
            "dalio" => horizon == 0
                ? "3년 안에 쓸 돈은 원래 주식보다 예금·단기 국채가 맞아요. 꼭 투자한다면 가장 덜 흔들리는 방식이 나아요."
                : "하락을 견디기 어렵다면 수익보다 '덜 잃는 것'이 먼저예요. 여러 자산을 섞어 가장 덜 흔들리는 방식이에요.",
            "buffett" => "오래 둘 수 있고 하락에도 흔들리지 않는다면, 미국 대표 지수에 대부분을 두는 게 가장 단순하고 강한 방법이에요.",
            _ => $"채권 {Math.Clamp(age, 20, 80)}%(나이만큼)로 흔들림을 줄이고, 나머지는 시장 전체를 사요. 고를 게 없어 오래 지키기 쉬워요.",
        };
        reason += "\n종목을 직접 골라 보고 싶다면 그레이엄·멍거·린치 방식을 눌러 보세요.";
        return (s, reason);
    }

    // 방식 → 구성. income의 개별주는 투자 계획과 같은 규칙(Plan.Satellites)으로 고른 것을 받는다.
    public static (List<Holding> list, List<string> notes) Build(Style s, List<StockRow> rows, Func<string, bool> maybeSpecial,
                                                                  List<StockRow> incomePicks, int age)
    {
        var notes = new List<string>();
        var kr = rows.Where(r => !r.Q.IsUs && r.Q.Price > 0 && r.Per3 != null).ToList();

        // 조건을 통과한 종목에서 n개 (같은 그룹으로 보이는 이름은 하나만)
        List<StockRow> Pick(Func<StockRow, bool> ok, Func<StockRow, double> order, int n)
        {
            var list = new List<StockRow>();
            foreach (var r in kr.Where(ok).Where(r => !maybeSpecial(r.Q.Code)).OrderBy(order))
            {
                if (list.Any(x => x.Q.Name.Length >= 2 && r.Q.Name.StartsWith(x.Q.Name[..2]))) continue;
                list.Add(r);
                if (list.Count == n) break;
            }
            return list;
        }

        // 주식 몫을 고른 종목에 똑같이 나눈다. 너무 적게 걸리면 남는 몫은 코스피200 ETF로.
        List<Holding> Stocks(List<StockRow> picks, double share, Func<StockRow, string> why, int want)
        {
            var h = new List<Holding>();
            if (kr.Count == 0) notes.Add("[가치 분석]을 아직 안 돌려서 종목을 고를 수 없어요. 주식 몫은 KODEX 200으로 채웠어요.");
            else if (picks.Count < want) notes.Add($"조건을 통과한 종목이 {picks.Count}개뿐이라 " + (picks.Count == 0 ? "주식 몫은 KODEX 200으로 채웠어요." : "그만큼만 담았어요."));
            if (picks.Count == 0) { h.Add(Etf("069500", share, "조건에 맞는 종목이 없어 한국 대표 지수로 대신")); return h; }
            foreach (var r in picks) h.Add(new(r.Q.Code, r.Q.Name, share / picks.Count, null, false, why(r)));
            return h;
        }

        bool Clean(StockRow r) => r.Labels.Contains("흑자 지속");
        var list = new List<Holding>();
        switch (s.Key)
        {
            case "income":
                list.Add(Etf("114260", Portfolio.Target[0], "흔들림을 줄이는 국고채"));
                list.Add(Etf("360750", Portfolio.Target[1], "미국 대표 기업 500개"));
                list.Add(Etf("458730", Portfolio.Target[2], "10년 넘게 배당한 미국 회사 100개, 매달 분배"));
                if (incomePicks.Count == 0) { list.Add(Etf("458730", Portfolio.Target[3], "")); notes.Add("[가치 분석] 결과가 없어 개별주 몫을 배당 ETF에 더했어요."); }
                else foreach (var r in incomePicks)
                    list.Add(new(r.Q.Code, r.Q.Name, Portfolio.Target[3] / incomePicks.Count, null, false, $"배당 {r.Div:0.0}% · 3년 PER {r.Per3:0.0}"));
                break;
            case "buffett":
                list.Add(Etf("360750", 0.9, "버핏이 권한 S&P500 인덱스"));
                list.Add(Etf("114260", 0.1, "단기 국채 대신 국고채 3년"));
                break;
            case "munger":
                list.Add(Etf("114260", 0.2, "앱이 더한 안전판"));
                list.AddRange(Stocks(Pick(r => Clean(r) && r.Roe >= 15 && r.DebtRatio <= 100 && r.Per3 <= 25, r => -(r.Roe ?? 0), 5), 0.8,
                    r => $"ROE {r.Roe:0}% · 부채 {r.DebtRatio:0}% · 3년 PER {r.Per3:0.0}", 5));
                break;
            case "graham":
                list.Add(Etf("114260", 0.5, "방어적 투자자의 채권 절반"));
                list.AddRange(Stocks(Pick(r => Clean(r) && r.Labels.Contains("재무건전") && r.Per3 <= 15 && r.Pbr != null && r.Per3 * r.Pbr <= 22.5 && r.DivYears >= 5,
                    r => (r.Per3 * r.Pbr) ?? 99, 10), 0.5, r => $"3년 PER {r.Per3:0.0} · PBR {r.Pbr:0.00} · 배당 {r.DivYears}년", 10));
                break;
            case "lynch":
                list.Add(Etf("114260", 0.2, "앱이 더한 안전판"));
                // 이익성장(%)은 처음 3년 평균 → 최근 3년 평균(대략 7년 차이)이라 1년 평균으로 바꿔 PEG를 만든다
                double? Peg(StockRow r) => r.Per > 0 && r.Growth > 0 ? r.Per / ((Math.Pow(1 + r.Growth.Value / 100, 1 / 7.0) - 1) * 100) : null;
                list.AddRange(Stocks(Pick(r => Peg(r) <= 1 && r.DebtRatio <= 100, r => Peg(r) ?? 99, 8), 0.8,
                    r => $"PEG {Peg(r):0.00} · PER {r.Per:0.0} · 이익 연 {(Math.Pow(1 + r.Growth.Value / 100, 1 / 7.0) - 1) * 100:0}% 성장", 8));
                break;
            case "bogle":
                double b = Math.Clamp(age, 20, 80) / 100.0;
                list.Add(Etf("114260", b, $"나이({age}세)만큼 채권"));
                list.Add(Etf("360750", (1 - b) * 2 / 3, "미국 시장 전체에 가까운 대표 지수"));
                list.Add(Etf("069500", (1 - b) / 3, "한국 대표 지수"));
                break;
            case "dalio":
                list.Add(Etf("360750", 0.30, "경기가 좋을 때 강한 주식"));
                list.Add(Etf("148070", 0.40, "경기가 나빠지고 금리가 내릴 때 강한 장기 국채"));
                list.Add(Etf("114260", 0.15, "중기 국채"));
                list.Add(Etf("411060", 0.15, "물가가 오를 때 강한 금 (원자재 몫 포함)"));
                break;
        }
        // 같은 코드가 두 번 들어간 경우(배당 ETF) 합친다
        list = list.GroupBy(h => h.Code).Select(g => g.First() with { Weight = g.Sum(h => h.Weight) }).ToList();
        return (list, notes);
    }
}
