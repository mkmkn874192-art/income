using System.Text.Json;
using System.Text.RegularExpressions;

namespace KrxScreen;

// 미국 종목 한글 별명 (애플, 엔비디아 ...). 사용자가 바꾼 별명은 nicknames.json에 저장해 기본값보다 먼저 쓴다.
// 영어 이름은 Quote.NameEn에 남겨 검색에 쓴다.
public static class Names
{
    static readonly string File_ = Path.Combine(Market.Dir, "nicknames.json");
    static Dictionary<string, string> user;

    static readonly Dictionary<string, string> Builtin = new(StringComparer.OrdinalIgnoreCase)
    {
        // 빅테크·반도체
        ["AAPL"] = "애플", ["NVDA"] = "엔비디아", ["MSFT"] = "마이크로소프트", ["GOOGL"] = "알파벳(구글) A", ["GOOG"] = "알파벳(구글) C",
        ["AMZN"] = "아마존", ["META"] = "메타", ["TSLA"] = "테슬라", ["AVGO"] = "브로드컴", ["TSM"] = "TSMC", ["MU"] = "마이크론",
        ["AMD"] = "AMD", ["ASML"] = "ASML", ["ORCL"] = "오라클", ["CSCO"] = "시스코", ["INTC"] = "인텔", ["AMAT"] = "어플라이드 머티어리얼즈",
        ["LRCX"] = "램리서치", ["TXN"] = "텍사스 인스트루먼트", ["PLTR"] = "팔란티어", ["PANW"] = "팔로알토 네트웍스", ["ANET"] = "아리스타 네트웍스",
        ["IBM"] = "IBM", ["CRWD"] = "크라우드스트라이크", ["DELL"] = "델", ["ARM"] = "ARM", ["KLAC"] = "KLA", ["APH"] = "암페놀",
        ["ADI"] = "아날로그 디바이스", ["QCOM"] = "퀄컴", ["CRM"] = "세일즈포스", ["ACN"] = "액센츄어", ["MRVL"] = "마벨", ["APP"] = "앱러빈",
        ["SNDK"] = "샌디스크", ["STX"] = "씨게이트", ["WDC"] = "웨스턴디지털", ["NET"] = "클라우드플레어", ["NOW"] = "서비스나우",
        ["SNOW"] = "스노우플레이크", ["ADBE"] = "어도비", ["FTNT"] = "포티넷", ["INTU"] = "인튜이트", ["CDNS"] = "케이던스", ["SNPS"] = "시놉시스",
        ["SAP"] = "SAP", ["SHOP"] = "쇼피파이", ["GLW"] = "코닝", ["MSI"] = "모토로라 솔루션", ["SKHY"] = "SK하이닉스(미국)", ["SPCX"] = "스페이스X",
        // 소비·유통·서비스
        ["WMT"] = "월마트", ["COST"] = "코스트코", ["KO"] = "코카콜라", ["PEP"] = "펩시코", ["PG"] = "P&G", ["MCD"] = "맥도날드",
        ["HD"] = "홈디포", ["LOW"] = "로우스", ["SBUX"] = "스타벅스", ["NFLX"] = "넷플릭스", ["DIS"] = "디즈니", ["NKE"] = "나이키",
        ["PM"] = "필립모리스", ["MO"] = "알트리아", ["BTI"] = "BAT(브리티시 아메리칸 토바코)", ["TJX"] = "TJX", ["BKNG"] = "부킹홀딩스",
        ["ABNB"] = "에어비앤비", ["UBER"] = "우버", ["SPOT"] = "스포티파이", ["MAR"] = "메리어트", ["HLT"] = "힐튼", ["RCL"] = "로열캐리비안",
        ["CL"] = "콜게이트", ["MDLZ"] = "몬델리즈", ["MNST"] = "몬스터 베버리지", ["BUD"] = "AB인베브", ["UL"] = "유니레버",
        ["BABA"] = "알리바바", ["PDD"] = "PDD(테무)", ["CVNA"] = "카바나", ["SONY"] = "소니", ["TM"] = "도요타", ["CMCSA"] = "컴캐스트",
        ["TMUS"] = "T모바일", ["VZ"] = "버라이즌", ["T"] = "AT&T", ["O"] = "리얼티 인컴",
        // 금융
        ["BRK-A"] = "버크셔 해서웨이 A", ["BRK-B"] = "버크셔 해서웨이 B", ["JPM"] = "JP모건", ["V"] = "비자", ["MA"] = "마스터카드",
        ["BAC"] = "뱅크오브아메리카", ["MS"] = "모건스탠리", ["GS"] = "골드만삭스", ["WFC"] = "웰스파고", ["C"] = "씨티그룹",
        ["AXP"] = "아메리칸 익스프레스", ["BLK"] = "블랙록", ["SCHW"] = "찰스 슈왑", ["BX"] = "블랙스톤", ["SPGI"] = "S&P 글로벌",
        ["PGR"] = "프로그레시브", ["CB"] = "처브", ["IBKR"] = "인터랙티브 브로커스", ["CME"] = "CME그룹", ["ICE"] = "ICE",
        ["COF"] = "캐피털원", ["USB"] = "US뱅코프", ["PNC"] = "PNC", ["BNY"] = "BNY멜론", ["KKR"] = "KKR", ["MCO"] = "무디스",
        ["TRV"] = "트래블러스", ["HSBC"] = "HSBC", ["UBS"] = "UBS", ["MUFG"] = "미쓰비시UFJ", ["SMFG"] = "미쓰이스미토모",
        ["RY"] = "캐나다 왕립은행", ["TD"] = "TD은행", ["SAN"] = "산탄데르", ["BBVA"] = "BBVA", ["HDB"] = "HDFC은행", ["IBN"] = "ICICI은행",
        // 헬스케어
        ["LLY"] = "일라이 릴리", ["JNJ"] = "존슨앤드존슨", ["ABBV"] = "애브비", ["UNH"] = "유나이티드헬스", ["MRK"] = "머크",
        ["NVS"] = "노바티스", ["AZN"] = "아스트라제네카", ["TMO"] = "써모피셔", ["AMGN"] = "암젠", ["GILD"] = "길리어드",
        ["ABT"] = "애보트", ["NVO"] = "노보 노디스크", ["PFE"] = "화이자", ["DHR"] = "다나허", ["ISRG"] = "인튜이티브 서지컬",
        ["VRTX"] = "버텍스", ["MDT"] = "메드트로닉", ["BMY"] = "브리스톨 마이어스", ["MCK"] = "맥케슨", ["SYK"] = "스트라이커",
        ["CVS"] = "CVS헬스", ["HCA"] = "HCA헬스케어", ["REGN"] = "리제네론", ["ELV"] = "엘레반스", ["MRNA"] = "모더나",
        ["SNY"] = "사노피", ["GSK"] = "GSK",
        // 산업·에너지·소재·유틸리티·부동산
        ["XOM"] = "엑슨모빌", ["CVX"] = "셰브론", ["GE"] = "GE 에어로스페이스", ["GEV"] = "GE 버노바", ["CAT"] = "캐터필러",
        ["RTX"] = "RTX(레이시온)", ["LMT"] = "록히드마틴", ["BA"] = "보잉", ["DE"] = "디어", ["UNP"] = "유니온퍼시픽",
        ["ETN"] = "이튼", ["PH"] = "파커하니핀", ["GD"] = "제너럴 다이내믹스", ["WM"] = "웨이스트 매니지먼트", ["CSX"] = "CSX",
        ["UPS"] = "UPS", ["MMM"] = "3M", ["TT"] = "트레인 테크놀로지스", ["EMR"] = "에머슨", ["PWR"] = "콴타 서비스",
        ["HWM"] = "하우멧", ["ITW"] = "일리노이 툴 웍스", ["JCI"] = "존슨 컨트롤즈", ["CTAS"] = "신타스", ["LIN"] = "린데",
        ["SHW"] = "셔윈윌리엄스", ["ECL"] = "에코랩", ["FCX"] = "프리포트 맥모란", ["NEM"] = "뉴몬트", ["SCCO"] = "서던 코퍼",
        ["BHP"] = "BHP", ["RIO"] = "리오틴토", ["SHEL"] = "셸", ["TTE"] = "토탈에너지스", ["BP"] = "BP", ["COP"] = "코노코필립스",
        ["EOG"] = "EOG", ["MPC"] = "마라톤 페트롤리엄", ["VLO"] = "발레로", ["PSX"] = "필립스66", ["ENB"] = "엔브리지",
        ["EPD"] = "엔터프라이즈 프로덕츠", ["WMB"] = "윌리엄스", ["NEE"] = "넥스트에라", ["SO"] = "서던컴퍼니", ["DUK"] = "듀크에너지",
        ["CEG"] = "컨스텔레이션 에너지", ["NGG"] = "내셔널그리드", ["PLD"] = "프로로지스", ["EQIX"] = "에퀴닉스", ["AMT"] = "아메리칸 타워",
        ["SPG"] = "사이먼 프로퍼티", ["WELL"] = "웰타워", ["ADP"] = "ADP",
        // 미국 ETF
        ["VOO"] = "뱅가드 S&P500", ["SPY"] = "SPDR S&P500", ["IVV"] = "아이셰어즈 S&P500", ["SPYM"] = "SPDR S&P500 저보수",
        ["VTI"] = "뱅가드 미국 전체 주식", ["ITOT"] = "아이셰어즈 미국 전체 주식", ["QQQ"] = "나스닥100 (QQQ)", ["QQQM"] = "나스닥100 (QQQM)",
        ["VXUS"] = "뱅가드 미국 외 전 세계", ["VEA"] = "뱅가드 선진국", ["VWO"] = "뱅가드 신흥국", ["IEFA"] = "아이셰어즈 선진국",
        ["IEMG"] = "아이셰어즈 신흥국", ["VEU"] = "뱅가드 미국 외 전 세계(VEU)", ["VT"] = "뱅가드 전 세계 주식",
        ["BND"] = "뱅가드 미국 채권 전체", ["BNDX"] = "뱅가드 해외 채권", ["AGG"] = "아이셰어즈 미국 채권 전체",
        ["SGOV"] = "미국 초단기 국채 (0~3개월)", ["TLT"] = "미국 장기 국채 (20년+)", ["IEF"] = "미국 중기 국채 (7~10년)", ["SHY"] = "미국 단기 국채 (1~3년)",
        ["VUG"] = "뱅가드 성장주", ["VTV"] = "뱅가드 가치주", ["VO"] = "뱅가드 중형주", ["VB"] = "뱅가드 소형주", ["VXF"] = "뱅가드 S&P500 외 전체",
        ["VGT"] = "뱅가드 IT", ["XLK"] = "SPDR 기술주", ["VIG"] = "뱅가드 배당 성장", ["VYM"] = "뱅가드 고배당", ["SCHD"] = "슈왑 미국 배당 (SCHD)",
        ["JEPI"] = "JP모건 월배당 커버드콜 (JEPI)", ["JEPQ"] = "JP모건 나스닥 커버드콜 (JEPQ)", ["DGRO"] = "아이셰어즈 배당 성장",
        ["GLD"] = "SPDR 금", ["IAU"] = "아이셰어즈 금", ["IJH"] = "아이셰어즈 중형주", ["IJR"] = "아이셰어즈 소형주",
        ["IWF"] = "러셀1000 성장주", ["IWD"] = "러셀1000 가치주", ["IWM"] = "러셀2000 소형주", ["DIA"] = "다우존스 (DIA)",
        ["RSP"] = "S&P500 동일 비중", ["SOXX"] = "아이셰어즈 반도체", ["SMH"] = "반에크 반도체", ["VNQ"] = "뱅가드 미국 리츠",
        ["DFAC"] = "DFA 미국 주식",
    };

    static Dictionary<string, string> User
    {
        get
        {
            if (user != null) return user;
            try { user = File.Exists(File_) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(File_)) : null; } catch { } // 깨졌으면 새로
            return user ??= new(StringComparer.OrdinalIgnoreCase);
        }
    }

    // 보여 줄 이름: 사용자 별명 → 기본 별명 → 영어 이름을 짧게 다듬은 것
    public static string Of(string code, string english)
    {
        if (User.TryGetValue(code, out var u) && !string.IsNullOrWhiteSpace(u)) return u;
        if (Builtin.TryGetValue(code, out var b)) return b;
        return Tidy(english);
    }

    public static bool HasNick(string code) => User.ContainsKey(code) || Builtin.ContainsKey(code);

    // 미국 시세에 한글 이름을 붙인다. 원래 영어 이름은 NameEn에.
    public static Quote Apply(Quote q) => !q.IsUs || q.NameEn != null ? q : q with { Name = Of(q.Code, q.Name), NameEn = q.Name };

    // 사용자가 정한 별명 (비우면 기본값으로)
    public static void Set(string code, string nick)
    {
        if (string.IsNullOrWhiteSpace(nick)) User.Remove(code); else User[code] = nick.Trim();
        try { Directory.CreateDirectory(Market.Dir); File.WriteAllText(File_, JsonSerializer.Serialize(User)); } catch { } // 저장 실패해도 이번 실행엔 적용
    }

    // 영어 회사 이름에서 Inc., Corporation, (The) 같은 꼬리를 뗀다
    static readonly Regex Tail = new(@"(,?\s+(Inc\.?|Incorporated|Corporation|Corp\.?|Company|Co\.?|plc\.?|PLC|Limited|Ltd\.?|Holdings?|Group|S\.A\.|N\.V\.|SE|AG|A/S|L\.P\.)|\s*\(The\)|\s+Common St.*|\s+Sponsor.*)+\s*$", RegexOptions.IgnoreCase);
    static string Tidy(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        string t = Tail.Replace(s.Trim(), "").Trim().TrimEnd(',');
        return t.Length >= 2 ? t : s.Trim();
    }

    // 좁은 자리(포메이션 등)용 짧은 이름: ETF 브랜드·꼬리말을 떼고, 그래도 길면 앞부분만
    static readonly string[] Brands = { "KODEX", "TIGER", "ACE", "KIWOOM", "KOSEF", "SOL", "RISE", "HANARO", "PLUS", "KBSTAR", "ARIRANG", "뱅가드", "아이셰어즈", "SPDR" };
    public static string Short(string name, int max = 7)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        string s = name.Trim();
        foreach (var b in Brands)
            if (s.StartsWith(b + " ", StringComparison.OrdinalIgnoreCase) && s.Length > b.Length + 2) { s = s[(b.Length + 1)..]; break; }
        s = Regex.Replace(s, @"\s*\((합성|H|UH|TR|[^)]*)\)\s*$", "");   // 괄호 꼬리
        s = Regex.Replace(s, @"(TR|액티브|채권혼합|커버드콜)$", "").Trim();
        s = s.Replace("S&P500", "S&P").Replace("국고채", "국채");
        return s.Length <= max ? s : s[..max] + "…";
    }
}
