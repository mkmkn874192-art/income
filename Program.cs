namespace KrxScreen;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // 화면 없이 가치 분석만 돌리고 저장 (작업 스케줄러로 매일 갱신할 때)
        if (args.Length > 0 && args[0] == "--analyze") return Analyze().GetAwaiter().GetResult();
        // 데이터 연결 점검 (Yahoo·한국은행·배당 기록이 받아지는지)
        if (args.Length > 0 && args[0] == "--check") return Check().GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    static async Task<int> Analyze()
    {
        var file = Path.Combine(Market.Dir, "keys.txt");
        var keys = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
        if (keys.Length < 2 || keys[0] == "" || keys[1] == "")
        {
            Console.WriteLine("keys.txt에 공공데이터포털 키와 OpenDART 키가 없어요.");
            return 1;
        }
        try
        {
            var screener = new Screener(keys[0], keys[1], 5000, 1e12, new ConsoleProgress());
            var t = await screener.Run();
            Market.SaveScreen(t, screener.Series);
            Console.WriteLine($"저장 완료: {Market.ScreenFile}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("오류: " + ex.Message);
            return 2;
        }
    }

    static async Task<int> Check()
    {
        var file = Path.Combine(Market.Dir, "keys.txt");
        var keys = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
        async Task Try(string name, Func<Task<string>> f)
        {
            try { Console.WriteLine($"[OK] {name}: {await f()}"); }
            catch (Exception ex) { Console.WriteLine($"[실패] {name}: {ex.Message.Split('\n')[0]}"); }
        }
        await Try("국장 업종·추세", async () =>
        {
            var t = await Yahoo.KrTrends();
            var s = t.GetValueOrDefault("005930");
            return $"{t.Count}종목, 삼성전자 {s?.Sector} 1년 {s?.Chg1y:0.0}% 고점대비 {s?.FromHigh:0.0}% 200일선 {s?.Vs200:0.0}%";
        });
        await Try("미장 시세·업종", async () =>
        {
            var (q, fx) = await Yahoo.Fetch();
            var a = q.FirstOrDefault(x => x.Code == "AAPL");
            return $"{q.Count}종목(업종 있는 것 {q.Count(x => x.Sector != null)}), 환율 {fx:N1}, AAPL {a?.Sector} 1년 {a?.Chg1y:0.0}%";
        });
        await Try("시장 환경", async () =>
        {
            var s = await Macro.Fetch(keys.Length > 6 ? keys[6] : null);
            return $"국고채3년 {s.Ktb3} (1년전 {s.Ktb3YearAgo}), 10년 {s.Ktb10}, 기준금리 {s.BaseRate}, 미10년 {s.Us10}, 미3개월 {s.Us3m}, " +
                   string.Join(", ", s.Indexes.Select(i => $"{i.Name} 1년 {i.Chg1y:0.0}% 고점 {i.FromHigh:0.0}%"));
        });
        await Try("시뮬레이터 (국고채3년 50 · S&P500 20 · 미국배당 20, 1천만원 + 월 100만원)", async () =>
        {
            var assets = new List<Backtest.Asset>
            {
                new("114260.KS", "KODEX 국고채3년", 50, null),
                new("360750.KS", "TIGER 미국S&P500", 20, "VOO"),
                new("458730.KS", "TIGER 미국배당다우존스", 20, "SCHD"),
            };
            var data = await Backtest.Load(assets);
            var sb = new System.Text.StringBuilder();
            foreach (var m in new[] { 12, 36, 60, 120 })
            {
                var r = Backtest.Run(assets, data, m, 1e7, 1e6, true, true);
                sb.Append($"\n   {m / 12}년: 원금 {r.Principal / 1e4:N0}만 → {r.Final / 1e4:N0}만, 연평균 {r.Cagr:0.0}%, 최대낙폭 {r.Mdd:0.0}%, 최악1년 {r.WorstYear:0.0}%, 배당 {r.DivTotal / 1e4:N0}만");
            }
            var fx = data["KRW=X"].Last().Value.close;
            var prices = assets.Select(a => data[a.Symbol].Last().Value.close).ToArray();
            var (min, slow) = Backtest.MinMonthly(assets, prices, 0.05);
            sb.Append($"\n   최소 월 적립 {min / 1e4:N0}만원 · " + string.Join(" / ", slow));
            sb.Append($"\n   참고: " + string.Join(" / ", Backtest.Run(assets, data, 120, 1e7, 1e6, true, true).Notes));
            return sb.ToString();
        });
        foreach (var sym in new[] { "O", "KO", "005930.KS", "458730.KS", "017800.KS" })
            await Try($"배당 기록 {sym}", async () =>
            {
                var d = Dividends.Analyze(await Yahoo.DividendHistory(sym));
                return $"{d.Freq} 연{d.PerYear}회, 최근1년 {d.Ttm:0.##}, 연속 {d.Years}년, 삭감 {d.Cuts}, 3년성장 {d.Growth3y:0.0}%, 특별 {d.Special}";
            });
        return 0;
    }

    // Progress<T>는 UI 스레드가 없으면 스레드풀로 넘기므로, 콘솔에는 바로 찍는다
    class ConsoleProgress : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine(value);
    }
}
