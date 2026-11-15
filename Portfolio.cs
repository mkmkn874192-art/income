namespace KrxScreen;

// 비중 구조: 기본 채권 ETF 50 / 미국지수 ETF 20 / 배당 ETF 20 / 개별주 10 (한 종목 ≤ 4%). 설정 화면에서 바꿀 수 있다.
public static class Portfolio
{
    public static readonly string[] Cats = { "채권", "지수", "배당", "개별주" };
    public static double[] Target = { 0.50, 0.20, 0.20, 0.10 };
    public static double Band = 0.05;
    public static double SatelliteMax = 0.04;

    static string TargetFile => Path.Combine(Market.Dir, "targets.txt");

    // targets.txt 한 줄: 채권,지수,배당,개별주,허용폭,한종목한도 (모두 %)
    public static void LoadTargets()
    {
        try
        {
            if (!File.Exists(TargetFile)) return;
            var v = File.ReadAllText(TargetFile).Split(',').Select(s => double.Parse(s.Trim()) / 100).ToArray();
            if (v.Length != 6 || Math.Abs(v.Take(4).Sum() - 1) > 0.001) return;
            Target = v.Take(4).ToArray();
            Band = v[4];
            SatelliteMax = v[5];
        }
        catch { } // 파일이 깨졌으면 기본값 사용
    }

    public static void SaveTargets()
    {
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllText(TargetFile, string.Join(",", Target.Append(Band).Append(SatelliteMax).Select(x => (x * 100).ToString("0.##"))));
    }

    public static readonly string ClassFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KrxScreen", "classes.txt");

    // classes.txt 한 줄: 코드,분류[,매수]  (분류 = 채권/지수/배당/개별주, '매수'가 붙은 코드를 그 분류의 적립 대상으로 사용)
    public record ClassEntry(string Cat, bool BuyTarget);

    public static Dictionary<string, ClassEntry> LoadClasses()
    {
        var map = new Dictionary<string, ClassEntry>();
        if (!File.Exists(ClassFile)) return map;
        foreach (var line in File.ReadAllLines(ClassFile))
        {
            var p = line.Split(',').Select(s => s.Trim()).ToArray();
            if (p.Length >= 2 && p[1] == "위성") p[1] = "개별주"; // 예전 이름
            if (p.Length < 2 || p[0].StartsWith("#") || !Cats.Contains(p[1])) continue;
            map[p[0]] = new ClassEntry(p[1], p.Length > 2 && p[2] == "매수");
        }
        return map;
    }

    // 키워드 자동 분류. 채권 > 배당 > 지수 순으로 먼저 걸리는 쪽. 나머지는 개별주.
    public static string AutoClass(string name)
    {
        string n = name.ToUpperInvariant();
        if (new[] { "국채", "국고채", "채권", "회사채", "통안", "단기채", "금리", "CD", "KOFR", "MMF", "머니마켓" }.Any(n.Contains)) return "채권";
        if (n.Contains("배당")) return "배당";
        if (new[] { "미국", "S&P", "나스닥", "NASDAQ", "다우", "러셀" }.Any(n.Contains)) return "지수";
        return "개별주";
    }

    public static string Classify(string code, string name, Dictionary<string, ClassEntry> map) =>
        map.TryGetValue(code, out var e) ? e.Cat : AutoClass(name);

    // 이번 달 금액 배분. 어느 분류든 ±5%p 넘게 벗어나면 부족분 채우기, 아니면 목표 비중대로.
    public static double[] Allocate(double[] current, double cash, out bool rebalancing)
    {
        double total = current.Sum() + cash;
        var alloc = new double[Cats.Length];
        if (total <= 0 || cash <= 0) { rebalancing = false; return alloc; }

        rebalancing = Enumerable.Range(0, Cats.Length).Any(i => Math.Abs(current[i] / total - Target[i]) > Band);
        if (!rebalancing)
        {
            for (int i = 0; i < Cats.Length; i++) alloc[i] = cash * Target[i];
            return alloc;
        }
        var deficit = Enumerable.Range(0, Cats.Length).Select(i => Math.Max(0, total * Target[i] - current[i])).ToArray();
        double sum = deficit.Sum();
        for (int i = 0; i < Cats.Length; i++) alloc[i] = cash * deficit[i] / sum; // total 기준이라 sum ≥ cash
        return alloc;
    }
}
