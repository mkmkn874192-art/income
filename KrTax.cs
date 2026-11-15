namespace KrxScreen;

// 한국 세금·4대 보험 계산 (2026년 기준). 숫자는 모두 이 파일 한 곳에서 관리한다.
// 근로소득: 연봉 → 4대 보험(근로자 부담) → 근로소득세(국세청 간이세액표) + 지방소득세 10% → 월 실수령
// 투자: 증권거래세, 배당·이자 원천징수, 해외주식 양도소득세, 국내 상장 ETF 매매차익 과세
public static class KrTax
{
    // ---------- 4대 보험 (근로자 부담분) — 게임이 실제 시간으로 흐르므로 날짜별로 ----------
    // 국민연금: 2025 개정법으로 2026년 9.5%부터 매년 0.5%p씩 올라 2033년 13% (근로자는 절반). 출처: 국민연금공단
    public static double PensionRate(DateTime d) => Math.Min(0.13, 0.095 + 0.005 * Math.Max(0, d.Year - 2026)) / 2;
    // 기준소득월액 상·하한: 매년 7월 바뀐다 (2025.7~2026.6: 40만~637만, 2026.7~2027.6: 41만~659만). 이후는 발표 전이라 마지막 값을 쓴다.
    public static (double min, double max) PensionBase(DateTime d) =>
        d < new DateTime(2026, 7, 1) ? (400_000, 6_370_000) : (410_000, 6_590_000);
    // 건강보험 7.19% (2026, 2027 동결) 중 근로자 절반. 근로자 몫 월 상한 4,591,740원·하한 10,080원 (2026 고시)
    public static double HealthRate(DateTime d) => d.Year < 2026 ? 0.03545 : 0.03595;
    public const double HealthMax = 4_591_740, HealthMin = 10_080;
    // 장기요양 = 건강보험료 × 비율 (2026 13.14%, 2027 13.90% 의결)
    public static double LtcOfHealth(DateTime d) => d.Year < 2026 ? 0.1295 : d.Year == 2026 ? 0.1314 : 0.1390;
    // 고용보험 실업급여 근로자 0.9% (2027년 1.0% 인상은 입법예고 단계라 확정 전까지 0.9%)
    public static double EmploymentRate(DateTime d) => 0.009;
    public const double MealNonTaxable = 200_000;

    // 직장가입자 소득월액보험료: 월급 말고 이자·배당 등이 한 해 2천만원을 넘으면 넘는 부분에 건강보험(전액 본인, 7.19%) + 장기요양을 더 낸다.
    // Y년 소득은 Y+1년 11월 ~ Y+2년 10월에 매달 걷는다. 월 상한 4,591,740원 (2026 고시)
    public const double ExtraIncomeThreshold = 20_000_000;
    public static double ExtraHealthPremium(double yearlyNonSalaryIncome, DateTime d)
    {
        if (yearlyNonSalaryIncome <= ExtraIncomeThreshold) return 0;
        double health = Math.Min(HealthMax, Math.Floor((yearlyNonSalaryIncome - ExtraIncomeThreshold) / 12 * HealthRate(d) * 2 / 10) * 10);
        return health + Math.Floor(health * LtcOfHealth(d) / 10) * 10;
    }
    public static int ExtraHealthIncomeYear(DateTime month) => month.Month >= 11 ? month.Year - 1 : month.Year - 2;      // 비과세 식대 월 20만원 (보험료·소득세 계산 모두에서 빠짐)

    // ---------- 근로소득세: 회사가 매달 떼는 금액 = 근로소득 간이세액표 (소득세법 시행령 별표2) ----------
    // 표는 Assets/ganise.csv (월급여액 770천원~10,000천원, 공제대상가족 1~11명). 2026.3.1 개정표는 기본 세액이 2023.2.28 표와 같고 자녀 차감액만 올랐다.
    public const double LocalRate = 0.10; // 지방소득세 = 소득세 × 10%
    static readonly double[] At10M = { 1_507_400, 1_431_570, 1_200_840, 1_170_840, 1_140_840, 1_110_840, 1_080_840, 1_050_840, 1_020_840, 990_840, 960_840 };
    static List<(int lo, int hi, int[] tax)> table;

    static List<(int lo, int hi, int[] tax)> Table
    {
        get
        {
            if (table != null) return table;
            table = new();
            using var st = typeof(KrTax).Assembly.GetManifestResourceStream("ganise.csv");
            if (st == null) return table;
            using var rd = new StreamReader(st);
            while (rd.ReadLine() is { } line)
            {
                if (line.StartsWith('#') || line.Length == 0) continue;
                var v = line.Split(',').Select(int.Parse).ToArray();
                table.Add((v[0], v[1], v[2..13]));
            }
            return table;
        }
    }

    // 간이세액표 월 소득세 (100% 선택). pay = 월급여액(비과세 제외, 원), family = 공제대상가족(본인·배우자 포함), kids = 그중 8세 이상 20세 이하 자녀
    public static double WithholdingTax(double pay, int family = 1, int kids = 0, DateTime? when = null)
    {
        family = Math.Max(1, family);
        double tax;
        if (pay < 770_000) tax = 0;
        else if (pay < 10_000_000) tax = Lookup(pay, family); // 표 (770천원~10,000천원 미만)
        else
        {
            double b = Fam(At10M, family), x = pay;
            tax = x <= 10_000_000 ? b
                : x <= 14_000_000 ? b + (x - 10_000_000) * 0.98 * 0.35 + 25_000
                : x <= 28_000_000 ? b + 1_397_000 + (x - 14_000_000) * 0.98 * 0.38
                : x <= 30_000_000 ? b + 6_610_600 + (x - 28_000_000) * 0.98 * 0.40
                : x <= 45_000_000 ? b + 7_394_600 + (x - 30_000_000) * 0.40
                : x <= 87_000_000 ? b + 13_394_600 + (x - 45_000_000) * 0.42
                : b + 31_034_600 + (x - 87_000_000) * 0.45;
        }
        // 8세 이상 20세 이하 자녀 차감 (2026.3.1~ 20,830 / 45,830 / 셋째부터 +33,330, 그 전 12,500 / 29,160 / +25,000)
        if (kids > 0)
        {
            bool y2026 = (when ?? DateTime.Now) >= new DateTime(2026, 3, 1);
            double one = y2026 ? 20_830 : 12_500, two = y2026 ? 45_830 : 29_160, more = y2026 ? 33_330 : 25_000;
            tax -= kids == 1 ? one : two + more * (kids - 2);
        }
        return Math.Max(0, Math.Floor(tax / 10) * 10);
    }

    static double Lookup(double pay, int family)
    {
        int k = (int)Math.Floor(pay / 1000); // 천원 단위
        var t = Table;
        int lo = 0, hi = t.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (k < t[mid].lo) hi = mid - 1;
            else if (k >= t[mid].hi) lo = mid + 1;
            else return Fam(t[mid].tax.Select(x => (double)x).ToArray(), family);
        }
        return 0;
    }

    // 가족 수 열 고르기. 11명 넘으면 (11명 세액) − (10명 세액 − 11명 세액) × 넘는 인원 (별표2 제4호)
    static double Fam(double[] row, int family) =>
        family <= 11 ? row[family - 1] : Math.Max(0, row[10] - (row[9] - row[10]) * (family - 11));

    // ---------- 투자 ----------
    public const double DividendTax = 0.154;           // 배당·이자 원천징수 14% + 지방 1.4%
    public const double InterestTax = 0.154;
    public const double UsDividendWithholding = 0.15;  // 미국 배당 원천징수 (한·미 조세조약). 금융소득 2천만원 이하면 국내 추가 세금 없음
    public const double FinancialIncomeThreshold = 20_000_000; // 금융소득 종합과세 기준 (연)
    public const double KospiSellTax = 0.0020;         // 증권거래세(농특세 포함), 팔 때만
    public const double KosdaqSellTax = 0.0020;
    public const double OverseasGainRate = 0.22;       // 해외주식 양도소득세 20% + 지방 2%
    public const double OverseasGainDeduction = 2_500_000; // 연 기본공제
    public const double EtfGainTax = 0.154;            // 국내 상장 기타 ETF(해외지수·채권·원자재 등) 매매차익 → 배당소득세

    public record Payroll(double Gross, double Pension, double Health, double Ltc, double Employment, double IncomeTax, double LocalTax)
    {
        public double Deductions => Pension + Health + Ltc + Employment + IncomeTax + LocalTax;
        public double Net => Gross - Deductions;
    }

    // 연봉(세전, 비과세 식대 포함) → 한 달 급여 명세. family = 공제대상가족(본인 포함), kids = 그중 8~20세 자녀, when = 어느 달 기준인지
    public static Payroll Monthly(double annualSalary, int family = 1, DateTime? when = null, bool mealAllowance = true, int kids = 0)
    {
        if (annualSalary <= 0) return new(0, 0, 0, 0, 0, 0, 0);
        var d = when ?? DateTime.Now;
        double gross = annualSalary / 12;
        double meal = mealAllowance ? Math.Min(MealNonTaxable, gross) : 0;
        double taxable = gross - meal; // 보험료·소득세 기준 (비과세 제외)

        var (bmin, bmax) = PensionBase(d);
        double pensionBase = Math.Floor(Math.Clamp(taxable, bmin, bmax) / 1000) * 1000; // 천원 미만 버림 (공단 기준)
        double pension = Math.Floor(pensionBase * PensionRate(d));
        double health = Math.Clamp(Math.Floor(taxable * HealthRate(d) / 10) * 10, HealthMin, HealthMax);
        double ltc = Math.Floor(health * LtcOfHealth(d) / 10) * 10;
        double employment = Math.Floor(taxable * EmploymentRate(d) / 10) * 10;

        double incomeTax = WithholdingTax(taxable, family, kids, d);
        double local = Math.Floor(incomeTax * LocalRate / 10) * 10;
        return new(gross, pension, health, ltc, employment, incomeTax, local);
    }

    // ---------- 투자 세금 ----------

    // 국내 주식을 팔 때 증권거래세 (ETF·ETN은 없음)
    public static double SellTax(string market, bool etf) => etf ? 0 : market == "KOSDAQ" ? KosdaqSellTax : KospiSellTax;

    // 국내 상장 ETF 중 매매차익이 비과세인 '국내 주식형'인지 (이름으로 판단하는 대략값)
    static readonly string[] Taxed = { "미국", "나스닥", "S&P", "글로벌", "차이나", "중국", "일본", "인도", "베트남", "유로", "선진국", "신흥국",
                                       "채권", "국채", "국고채", "회사채", "금리", "단기", "머니마켓", "골드", "금현물", "원유", "달러", "엔화", "리츠", "TDF", "혼합" };
    public static bool EtfGainTaxed(string name) => Taxed.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
}
