using System.Text.Json;
using System.Text.Json.Serialization;

namespace KrxScreen;

// 라이프: 구단주(월급 등)에게 받는 돈과 매달 나가는 지출. 월 배당이 쌓이면 지출이 싼 것부터 하나씩 '해금'된다.
public class CartItem
{
    public string Name { get; set; }
    public double Price { get; set; }      // 한 번 쓰는 돈 (원)
    public double Times { get; set; } = 1; // 한 달에 몇 번
    [JsonIgnore] public double Monthly => Price * Times;
}

public static class Cart
{
    public record Preset(string Name, double Price, double Times, string Note);

    // 이름 칸 자동완성용 예시 (지출 항목은 사용자가 직접 만든다)
    public static readonly Preset[] Presets =
    {
        new("커피", 4_500, 22, "하루 한 잔 · 출근일 22일"),
        new("점심", 10_000, 22, "출근일 22일"),
        new("OTT 구독", 17_000, 1, "한 달"),
        new("통신비", 60_000, 1, "한 달"),
        new("교통비", 70_000, 1, "한 달"),
        new("공과금", 120_000, 1, "전기·가스·수도"),
        new("용돈", 300_000, 1, "한 달"),
        new("월세", 500_000, 1, "한 달"),
    };

    class State
    {
        public List<CartItem> Items { get; set; } = new();
        public int Unlocked { get; set; } // 지난번에 해금돼 있던 개수 (새로 해금되면 축하하려고)
        public double Income { get; set; } // 구단주(월급 등)에게 받는 한 달 수입 (원)
    }

    static string File_ => Path.Combine(Market.Dir, "cart.json");
    static State state;
    static State S => state ??= Load();

    static State Load()
    {
        try { return File.Exists(File_) ? JsonSerializer.Deserialize<State>(File.ReadAllText(File_)) ?? new() : new(); }
        catch { return new(); } // 깨졌으면 빈 장바구니
    }

    static void Save()
    {
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(S));
    }

    public static List<CartItem> Items => S.Items;
    public static double Income { get => S.Income; set { S.Income = value; Save(); } }

    public static void Add(CartItem item)
    {
        var same = S.Items.FirstOrDefault(i => i.Name == item.Name);
        if (same != null) { same.Price = item.Price; same.Times = item.Times; } else S.Items.Add(item);
        Save();
    }

    public static void Remove(IEnumerable<string> names)
    {
        var set = names.ToHashSet();
        S.Items.RemoveAll(i => set.Contains(i.Name));
        Save();
    }

    public record Row(CartItem Item, double Covered, bool Done, double Need);

    // 싼 것부터 월 배당을 채워 나간다. Covered = 이 항목에 쓸 수 있는 배당(0~월 금액), Need = 해금까지 더 필요한 월 배당
    public static List<Row> Cover(double monthlyDiv)
    {
        var rows = new List<Row>();
        double left = monthlyDiv;
        foreach (var i in S.Items.OrderBy(i => i.Monthly))
        {
            double c = Math.Clamp(left, 0, Math.Max(0, i.Monthly)); // 금액이 음수로 저장돼 있어도 오류 없이
            left -= c;
            rows.Add(new Row(i, c, c >= i.Monthly - 0.5, i.Monthly - c));
        }
        return rows;
    }

    // 새로 해금된 개수만큼 돌려주고 기록을 갱신한다
    public static int NewlyUnlocked(int unlockedNow)
    {
        int gained = unlockedNow - S.Unlocked;
        if (gained != 0) { S.Unlocked = unlockedNow; Save(); }
        return Math.Max(0, gained);
    }
}
