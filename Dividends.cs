using System.Text.Json;

namespace KrxScreen;

// 배당 지급 기록(Yahoo, 최근 10년)으로 본 배당 주기와 안정성.
// Freq: 월 / 분기 / 반기 / 연 / 불규칙 / 없음
public record DivInfo(string Freq, int PerYear, double Ttm, int Years, int Cuts, double? Growth3y, bool Special, DateTime Fetched)
{
    // 주기 점수: 자주 줄수록 현금 흐름이 고르고 재투자가 빨라진다
    public int FreqRank => Freq switch { "월" => 4, "분기" => 3, "반기" => 2, "연" => 1, _ => 0 };
}

public static class Dividends
{
    static string File_ => Path.Combine(Market.Dir, "dividends.json");
    static Dictionary<string, DivInfo> cache;

    public static Dictionary<string, DivInfo> All => cache ??= Load();

    static Dictionary<string, DivInfo> Load()
    {
        try { return File.Exists(File_) ? JsonSerializer.Deserialize<Dictionary<string, DivInfo>>(File.ReadAllText(File_)) : new(); }
        catch { return new(); }
    }

    static void Save()
    {
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(All));
    }

    public static DivInfo Get(string symbol) => All.GetValueOrDefault(symbol);

    public static DivInfo Analyze(List<Yahoo.Point> pays)
    {
        if (pays.Count == 0) return new DivInfo("없음", 0, 0, 0, 0, null, false, DateTime.Now);
        int lastFull = DateTime.Today.Year - 1;

        // 특별배당: 최근 5년 지급액 중앙값의 2.5배를 넘는 지급
        var recent = pays.Where(p => p.Date.Year >= lastFull - 4).Select(p => p.Close).OrderBy(x => x).ToList();
        double median = recent.Count > 0 ? recent[recent.Count / 2] : 0;
        bool IsSpecial(Yahoo.Point p) => median > 0 && p.Close > median * 2.5;
        var regular = pays.Where(p => !IsSpecial(p)).ToList();

        var byYear = regular.GroupBy(p => p.Date.Year).ToDictionary(g => g.Key, g => (n: g.Count(), sum: g.Sum(p => p.Close)));

        // 주기: 최근 3개 완결 연도의 지급 횟수 중앙값
        var counts = Enumerable.Range(lastFull - 2, 3).Select(y => byYear.TryGetValue(y, out var v) ? v.n : 0).OrderBy(x => x).ToList();
        int per = counts[1];
        string freq = per >= 10 ? "월" : per >= 3 ? "분기" : per == 2 ? "반기" : per == 1 ? "연" : regular.Count > 0 ? "불규칙" : "없음";

        // 최근 1년(365일) 정기 배당 합
        double ttm = regular.Where(p => p.Date > DateTime.Today.AddDays(-365)).Sum(p => p.Close);

        // 연속 배당 연수 (작년부터 거꾸로)
        int years = 0;
        for (int y = lastFull; byYear.ContainsKey(y); y--) years++;

        // 삭감: 최근 5년 중 전년보다 연간 정기 배당이 10% 넘게 준 해
        int cuts = 0;
        for (int y = lastFull - 4; y <= lastFull; y++)
            if (byYear.TryGetValue(y, out var cur) && byYear.TryGetValue(y - 1, out var prev) && cur.sum < prev.sum * 0.9) cuts++;

        double? growth = byYear.TryGetValue(lastFull, out var a) && byYear.TryGetValue(lastFull - 3, out var b) && b.sum > 0
            ? (Math.Pow(a.sum / b.sum, 1 / 3.0) - 1) * 100 : null;

        bool special = pays.Any(p => p.Date.Year >= lastFull - 1 && IsSpecial(p));
        return new DivInfo(freq, per, ttm, years, cuts, growth, special, DateTime.Now);
    }

    public static async Task<DivInfo> Fetch(string symbol)
    {
        var info = Analyze(await Yahoo.DividendHistory(symbol));
        lock (All) All[symbol] = info;
        return info;
    }

    // 여러 종목을 4개씩 동시에. 1주일 안에 받은 건 건너뛴다. 요청 한도(429)에 걸리면 멈추고 다음에 이어서.
    public static async Task FetchMany(IList<string> symbols, IProgress<string> log)
    {
        var todo = symbols.Where(s => !All.TryGetValue(s, out var d) || d.Fetched < DateTime.Now.AddDays(-7)).Distinct().ToList();
        int done = 0;
        using var gate = new SemaphoreSlim(4);
        bool limited = false;
        var tasks = todo.Select(async s =>
        {
            await gate.WaitAsync();
            try
            {
                if (limited) return;
                await Fetch(s);
                if (Interlocked.Increment(ref done) % 50 == 0) { log?.Report($"배당 기록 {done}/{todo.Count}"); lock (All) Save(); }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests) { limited = true; }
            catch { } // 한 종목 실패는 넘어간다
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
        lock (All) Save();
        log?.Report(limited ? $"배당 기록 {done}/{todo.Count} (요청 한도로 일부는 다음에)" : $"배당 기록 {done}개 갱신");
    }
}
