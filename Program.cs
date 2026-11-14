namespace KrxScreen;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // 화면 없이 가치 분석만 돌리고 저장 (작업 스케줄러로 매일 갱신할 때)
        if (args.Length > 0 && args[0] == "--analyze") return Analyze().GetAwaiter().GetResult();

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

    // Progress<T>는 UI 스레드가 없으면 스레드풀로 넘기므로, 콘솔에는 바로 찍는다
    class ConsoleProgress : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine(value);
    }
}
