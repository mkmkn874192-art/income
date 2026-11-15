namespace KrxScreen;

// 돈 표시는 여기 한 곳에서: 한국식 단위(1조 2,993억원), 달러 → 원화 보기 설정
public static class Won
{
    static readonly string File_ = Path.Combine(Market.Dir, "display.txt");
    static bool? usdInKrw;

    // 설정: 미국 종목 가격도 원화로 보기 (환율은 앱이 받은 최신값)
    public static bool UsdInKrw
    {
        get
        {
            if (usdInKrw == null)
                try { usdInKrw = File.Exists(File_) && File.ReadAllText(File_).Contains("usd_in_krw=1"); } catch { usdInKrw = false; }
            return usdInKrw.Value;
        }
        set
        {
            usdInKrw = value;
            try { Directory.CreateDirectory(Market.Dir); File.WriteAllText(File_, $"usd_in_krw={(value ? 1 : 0)}"); } catch { } // 저장 실패는 이번 실행에만 적용
        }
    }

    // 원화 금액: 1조 2,993억원 · 3억 2,000만원 · 4만 5,000원 · 900원
    public static string Krw(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
        if (v < 0) return "-" + Krw(-v);
        v = Math.Round(v);
        if (v >= 1e12)
        {
            double eok = Math.Floor(v % 1e12 / 1e8);
            return $"{Math.Floor(v / 1e12):N0}조" + (eok > 0 ? $" {eok:N0}억" : "") + "원";
        }
        if (v >= 1e8)
        {
            double man = Math.Floor(v % 1e8 / 1e4);
            return $"{Math.Floor(v / 1e8):N0}억" + (man > 0 ? $" {man:N0}만" : "") + "원";
        }
        if (v >= 1e4)
        {
            double rest = v % 1e4;
            return $"{Math.Floor(v / 1e4):N0}만" + (rest > 0 ? $" {rest:N0}" : "") + "원";
        }
        return $"{v:N0}원";
    }

    // 시가총액처럼 큰 금액(원)을 짧게: 6,918조 154억 · 2,993억 (원 생략, 표에서 씀)
    public static string Big(double krw)
    {
        if (krw <= 0) return "";
        double jo = Math.Floor(krw / 1e12), eok = Math.Floor(krw % 1e12 / 1e8);
        if (jo > 0) return $"{jo:N0}조" + (eok > 0 ? $" {eok:N0}억" : "");
        return eok > 0 ? $"{eok:N0}억" : $"{Math.Floor(krw / 1e4):N0}만";
    }

    // 주가 한 개: 국내는 원, 미국은 설정에 따라 $ 또는 원
    public static string Price(double v, string currency)
    {
        if (currency != "USD") return $"{v:N0}원";
        return UsdInKrw ? $"{v * Market.UsdKrw:N0}원" : $"${v:N2}";
    }

    // 미국 종목 가격을 원화로 바꿔 보여 줄 때 덧붙이는 설명
    public static string FxNote(string currency) => currency == "USD" && UsdInKrw ? $" (환율 {Market.UsdKrw:N0}원 적용)" : "";
}
