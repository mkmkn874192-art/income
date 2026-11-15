using System.Text.Json;

namespace KrxScreen;

// 영어 회사 소개를 한국어로. 무료 비공식 번역 주소(translate.googleapis.com, client=gtx)라 키가 필요 없지만
// 막히거나 바뀔 수 있다. 실패하면 원문을 그대로 쓴다. 번역한 글은 translations.json에 저장해 다시 받지 않는다.
public static class Translate
{
    static readonly HttpClient http = Screener.NewHttp();
    static readonly string File_ = Path.Combine(Market.Dir, "translations.json");
    static Dictionary<string, string> cache;
    public static string LastError; // 마지막 실패 이유 (점검용)

    static Dictionary<string, string> Cache
    {
        get
        {
            if (cache != null) return cache;
            try { cache = File.Exists(File_) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(File_)) : null; }
            catch { } // 깨졌으면 새로 시작
            return cache ??= new();
        }
    }

    // 한글이 거의 없고 영문이 대부분인 글만 번역
    public static bool IsEnglish(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.Count(char.IsAsciiLetter) > s.Length / 2 && !s.Any(c => c is >= '가' and <= '힣');

    public static async Task<string> ToKorean(string text)
    {
        if (!IsEnglish(text)) return text;
        lock (Cache) if (Cache.TryGetValue(text, out var hit)) return hit;
        try
        {
            string ko;
            try
            {
                // 크롬 확장용 주소. 응답: ["번역"]
                var txt = await http.GetStringAsync("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=en&tl=ko&q=" + Uri.EscapeDataString(text));
                using var doc = JsonDocument.Parse(txt);
                var first = doc.RootElement[0];
                ko = first.ValueKind == JsonValueKind.Array ? first[0].GetString() : first.GetString();
            }
            catch (HttpRequestException)
            {
                // 예비: gtx 주소 (Windows 기본 통신에서는 429로 막히는 경우가 있다). 응답: [[["번역","원문",...], ...], ...]
                var txt = await http.GetStringAsync("https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ko&dt=t&q=" + Uri.EscapeDataString(text));
                using var doc = JsonDocument.Parse(txt);
                ko = string.Concat(doc.RootElement[0].EnumerateArray().Select(p => p[0].GetString()));
            }
            if (string.IsNullOrWhiteSpace(ko)) return text;
            lock (Cache)
            {
                Cache[text] = ko;
                Directory.CreateDirectory(Market.Dir);
                File.WriteAllText(File_, JsonSerializer.Serialize(Cache));
            }
            return ko;
        }
        catch (Exception ex) { LastError = ex.Message; return text; }
    }
}
