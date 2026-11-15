using System.Drawing.Drawing2D;

namespace KrxScreen;

// 종목을 축구 게임 선수 카드처럼: 능력치 1~20, 별점, 포지션, 스카우트 의견.
// 능력치는 지나간 숫자를 정해진 구간에 맞춰 바꾼 참고용이다.
public static class Scout
{
    public record Attr(string Name, int? Value, string Hint);

    static int Lin(double v, double lo, double hi) => (int)Math.Round(Math.Clamp(1 + (v - lo) / (hi - lo) * 19, 1, 20));

    static readonly string[] EtfBrands = { "KODEX", "TIGER", "ACE", "KIWOOM", "KOSEF", "SOL", "RISE", "HANARO", "PLUS", "KBSTAR", "ARIRANG" };
    public static bool IsEtf(StockRow r) => r.Q.Market == "US ETF" || EtfBrands.Any(b => r.Q.Name.StartsWith(b, StringComparison.OrdinalIgnoreCase));

    // vol = 1년 기준 가격 변동성(%, 월별 등락으로 계산). 없으면 '안정감'은 '–'.
    public static List<Attr> Attributes(StockRow r, double? ttmYield = null, double? vol = null)
    {
        var a = new List<Attr>();
        double? y = r.Yield ?? r.Div ?? ttmYield;
        a.Add(new("배당력", y is double yv ? Lin(yv, 0, 6) : null, y is double y2 ? $"배당률 {y2:0.0}%" : "자료 없음"));

        if (r.DivI is { Freq: not "없음" } d)
            a.Add(new("배당 꾸준함", Math.Clamp((int)Math.Round(Math.Min(d.Years, 10) / 10.0 * 14 + (d.Cuts == 0 && d.Years > 0 ? 4 : 0) + (d.Special ? 0 : 2)), 1, 20),
                      $"연속 {d.Years}년" + (d.Cuts > 0 ? $", 삭감 {d.Cuts}번" : "")));
        else if (r.DivYears is int dy)
            a.Add(new("배당 꾸준함", Math.Clamp((int)Math.Round(Math.Min(dy, 10) / 10.0 * 16 + (r.Labels.Contains("흑자 지속") ? 4 : 0)), 1, 20), $"배당 {dy}년"));
        else a.Add(new("배당 꾸준함", null, "자료 없음"));

        double? per = r.Per3 ?? r.Per;
        a.Add(new("가격 매력", per is double p ? (p <= 6 ? 20 : Lin(-p, -40, -6)) : null, per is double p2 ? $"{(r.Per3 != null ? "3년 " : "")}PER {p2:0.0}" : "자료 없음"));
        a.Add(new("돈 버는 힘", r.Roe is double roe ? Lin(roe, 0, 25) : null, r.Roe is double roe2 ? $"ROE {roe2:0}%" : "자료 없음"));
        a.Add(new("재무 체력", r.DebtRatio is double debt ? Lin(-debt, -200, 0) : null, r.DebtRatio is double debt2 ? $"부채비율 {debt2:0}%" : "자료 없음"));

        // 이익성장(%)은 처음 3년 평균 → 최근 3년 평균(대략 7년)이라 1년 평균으로 바꾼다
        double? g = r.Growth is double gr && gr > -100 ? (Math.Pow(1 + gr / 100, 1 / 7.0) - 1) * 100 : null;
        a.Add(new("성장", g is double g1 ? Lin(g1, -5, 15) : null, g is double g2 ? $"이익 연 {g2:+0;-0}%" : "자료 없음"));
        a.Add(new("요즘 폼", r.Q.Vs200 is double v ? Lin(v, -25, 25) : null, r.Q.Vs200 is double v2 ? $"200일선 대비 {v2:+0;-0}%" : "자료 없음"));

        a.Add(new("안정감", vol is double vv ? Lin(-vv, -40, -3) : null, vol is double v3 ? $"1년 변동성 {v3:0}%" : "자료 없음"));
        a.Add(IsEtf(r) ? new("분산", 20, "여러 종목을 한 번에") : new("분산", 3, "한 회사에 집중"));

        double capKrw = r.Q.Cap * Market.Fx(r.Q.Currency);
        a.Add(new("덩치", capKrw > 0 ? Math.Clamp((int)Math.Round(10 + 5 * Math.Log10(capKrw / 1e12)), 1, 20) : null,
                  capKrw > 0 ? $"시총 {Won.Big(capKrw)}" : "자료 없음"));
        return a;
    }

    // 포지션: 채권 = 수비수(지키는 돈), 배당·지수 = 미드필더(꾸준히 굴리는 돈), 성장주 = 공격수(키우는 돈)
    public static (string pos, string role, Color color) Position(StockRow r, List<Attr> a)
    {
        string n = r.Q.Name;
        int V(string name) => a.FirstOrDefault(x => x.Name == name)?.Value ?? 0;
        if (new[] { "국채", "국고채", "채권", "단기", "금리", "통안" }.Any(n.Contains)) return ("수비수", "채권 · 지키는 돈", Theme.CatColors[0]);
        if (n.Contains("금현물") || n.Contains("골드")) return ("수비수", "금 · 물가 대비", Color.FromArgb(234, 179, 8));
        if (V("배당력") >= 12) return ("미드필더", "배당 · 매달 굴리는 돈", Theme.CatColors[2]);
        if (n.Contains("200") || n.Contains("S&P") || n.Contains("나스닥") || r.Q.Market == "US ETF") return ("미드필더", "지수 · 시장 전체", Theme.CatColors[1]);
        if (V("성장") >= 12 || V("요즘 폼") >= 14) return ("공격수", "성장 · 키우는 돈", Theme.Up);
        return ("공격수", "개별주", Theme.CatColors[3]);
    }

    // 능력치 묶음 (보여 주는 순서)
    public static readonly (string Group, string Hint, string[] Names)[] Groups =
    {
        ("배당", "돈을 얼마나, 얼마나 꾸준히 주나", new[] { "배당력", "배당 꾸준함" }),
        ("실력", "회사가 돈을 벌고 키우는 힘, 가격", new[] { "돈 버는 힘", "성장", "재무 체력", "가격 매력" }),
        ("컨디션", "요즘 흐름과 흔들림, 몸집", new[] { "요즘 폼", "안정감", "덩치", "분산" }),
    };

    // 능력 5~100: '분산'을 뺀 알려진 능력치 평균 × 5. 아는 능력치가 4개 미만이면 매기지 않는다.
    public static int? Overall(List<Attr> a)
    {
        var known = a.Where(x => x.Value != null && x.Name != "분산").Select(x => x.Value.Value).ToList();
        return known.Count < 4 ? null : (int)Math.Round(known.Average() * 5);
    }

    // 별점 0~5 (반 개 단위). 종합 점수가 있으면 그걸로, 없으면 능력치 평균으로.
    public static double Stars(StockRow r, List<Attr> a)
    {
        double s = r.Total is double t ? t / 20 : a.Where(x => x.Value != null).Select(x => x.Value.Value).DefaultIfEmpty(10).Average() / 4;
        return Math.Round(Math.Clamp(s, 0, 5) * 2) / 2;
    }

    public static string Report(List<Attr> a, double stars)
    {
        var good = a.Where(x => x.Value >= 15).OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Name}({x.Hint})").ToList();
        var bad = a.Where(x => x.Value <= 6).OrderBy(x => x.Value).Take(3).Select(x => $"{x.Name}({x.Hint})").ToList();
        string verdict = stars >= 4 ? "주전감이에요. 구성의 중심으로 생각해 볼 만해요."
                       : stars >= 3 ? "쓸 만한 선수예요. 약한 부분을 다른 종목이 메워주면 좋아요."
                       : stars >= 2 ? "아직은 벤치 쪽이에요. 조금 더 지켜보세요."
                       : "지금 숫자로는 추천하기 어려워요.";
        var lines = new List<string> { "■ 스카우트 의견", verdict };
        if (good.Count > 0) lines.Add("• 강점: " + string.Join(", ", good));
        if (bad.Count > 0) lines.Add("• 약점: " + string.Join(", ", bad));
        int unknown = a.Count(x => x.Value == null);
        if (unknown > 0) lines.Add($"• 자료가 없어 '–'로 둔 능력치가 {unknown}개 있어요" + (unknown >= 4 ? " (ETF나 가치 분석 전 종목은 재무 자료가 없어서, 있는 능력치로만 별점을 매겨요)" : "."));
        lines.Add("능력치는 지나간 숫자를 1~20으로 바꾼 참고용이고, 앞으로를 보장하지 않아요.");
        return string.Join("\n", lines);
    }

    // 축구 게임처럼 숫자 구간별 색: 1~5 빨강, 6~10 주황, 11~15 연두, 16~20 초록
    public static Color AttrColor(int v) => v <= 5 ? Color.FromArgb(239, 68, 68) : v <= 10 ? Color.FromArgb(251, 146, 60)
                                         : v <= 15 ? Color.FromArgb(163, 230, 53) : Color.FromArgb(34, 197, 94);
}

// 능력치 목록: 이름 · 색 막대 · 숫자 상자
class AttrList : Control
{
    List<Scout.Attr> attrs = new();

    public AttrList()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.CardBg;
    }

    public void SetData(List<Scout.Attr> a) { attrs = a; Invalidate(); }

    // 묶음 제목(배당·실력·컨디션)과 능력치 줄을 보여 줄 순서대로. 제목 줄은 Attr가 null.
    List<(string head, Scout.Attr attr)> Lines()
    {
        var lines = new List<(string, Scout.Attr)>();
        var used = new HashSet<string>();
        foreach (var (grp, hint, names) in Scout.Groups)
        {
            var mine = names.Select(n => attrs.FirstOrDefault(a => a.Name == n)).Where(a => a != null).ToList();
            if (mine.Count == 0) continue;
            lines.Add(($"{grp}  ·  {hint}", null));
            foreach (var a in mine) { lines.Add((null, a)); used.Add(a.Name); }
        }
        foreach (var a in attrs.Where(a => !used.Contains(a.Name))) lines.Add((null, a));
        return lines;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        int rowH = (int)(30 * s), headH = (int)(24 * s), box = (int)(28 * s), nameW = (int)(92 * s);
        if (Width - nameW - box - 12 * s < 10 * s) return; // 너무 좁으면 그리지 않는다 (둥근 막대 크기가 음수가 되면 오류)
        int y = 0;
        foreach (var (head, a) in Lines())
        {
            if (head != null)
            {
                TextRenderer.DrawText(g, head, Theme.Small, new Rectangle(0, y, Width, headH), Theme.Muted, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
                y += headH;
                continue;
            }
            TextRenderer.DrawText(g, a.Name, Theme.Bold, new Rectangle(0, y, nameW, rowH), Theme.Text, TextFormatFlags.VerticalCenter);
            // 막대: 1~20 비율
            float bx = nameW, bw = Width - nameW - box - 12 * s, by = y + rowH / 2f - 4 * s;
            using (var track = new SolidBrush(Theme.InputBg)) using (var tp = Theme.Round(new RectangleF(bx, by, bw, 8 * s), 4 * s)) g.FillPath(track, tp);
            if (a.Value is int v)
            {
                using var fill = new SolidBrush(Scout.AttrColor(v));
                using var fp = Theme.Round(new RectangleF(bx, by, Math.Max(8 * s, bw * v / 20f), 8 * s), 4 * s);
                g.FillPath(fill, fp);
            }
            // 숫자 상자
            var rb = new RectangleF(Width - box - 2, y + (rowH - box) / 2f, box, box);
            var c = a.Value is int v2 ? Scout.AttrColor(v2) : Theme.Disabled;
            using (var bp = Theme.Round(rb, 8 * s)) using (var bb = new SolidBrush(c)) g.FillPath(bb, bp);
            TextRenderer.DrawText(g, a.Value?.ToString() ?? "–", Theme.Bold, Rectangle.Round(rb), a.Value == null ? Theme.Muted : Color.FromArgb(20, 20, 20),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            y += rowH;
        }
    }

    public override Size GetPreferredSize(Size proposed)
    {
        float s = DeviceDpi / 96f;
        var lines = Lines();
        return new(proposed.Width, (int)(lines.Count(l => l.head != null) * 24 * s + lines.Count(l => l.head == null) * 30 * s));
    }
}

// 선수 카드 창. 한 번에 하나만 띄우고, 다른 종목을 누르면 내용만 바뀐다.
class PlayerCard : Form
{
    static PlayerCard current;
    readonly Label name = new() { Dock = DockStyle.Top, Height = 38, Font = Theme.H1, ForeColor = Theme.Text, AutoEllipsis = true };
    readonly Label sub = new() { Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted };
    readonly Label price = new() { Dock = DockStyle.Top, Height = 30, Font = Theme.H2, ForeColor = Theme.Text };
    readonly Label stars = new() { Dock = DockStyle.Top, Height = 40, Font = new Font(Theme.H1.FontFamily, 18F), ForeColor = Color.FromArgb(250, 204, 21), TextAlign = ContentAlignment.MiddleRight };
    readonly Label overall = new() { Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleRight };
    readonly Pill pos = new("", Theme.Accent) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
    readonly AttrList attrs = new() { Dock = DockStyle.Fill };
    readonly PriceChart chart = new() { Dock = DockStyle.Fill };
    readonly TermText report = new() { Dock = DockStyle.Fill };
    readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0), BackColor = Theme.Bg };
    int version;

    PlayerCard()
    {
        // 아래 픽셀 값은 100% 배율 기준. ResumeLayout 때 화면 배율만큼 키운다 (MainForm과 같은 방식)
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(900, 880);
        MinimumSize = new Size(760, 700);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Padding = new Padding(16);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Theme.DarkTitleBar(this);

        var right = new Panel { Dock = DockStyle.Right, Width = 220 };
        var posRow = new Panel { Dock = DockStyle.Top, Height = 34 };
        posRow.Controls.Add(pos);
        posRow.Resize += (_, _) => pos.Left = posRow.Width - pos.Width;
        right.Controls.Add(posRow);
        right.Controls.Add(overall);
        right.Controls.Add(stars);
        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(price);
        left.Controls.Add(sub);
        left.Controls.Add(name);
        var head = new Panel { Dock = DockStyle.Top, Height = 106 };
        head.Controls.Add(left);
        head.Controls.Add(right);

        var mid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 450, ColumnCount = 2, Margin = Padding.Empty };
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        mid.Controls.Add(new Card("능력치 (1~20)", attrs), 0, 0);
        mid.Controls.Add(new Card("최근 1년 주가", chart), 1, 0);

        Controls.Add(new Card("스카우트 리포트 · 회사 소개", report) { Margin = Padding.Empty });
        Controls.Add(Theme.Spacer(12));
        Controls.Add(mid);
        Controls.Add(Theme.Spacer(8));
        Controls.Add(head);
        Controls.Add(actions);
        FormClosed += (_, _) => current = null;
        ResumeLayout(false);
    }

    public record Data(StockRow Row, double? TtmYield, double? Vol, Func<Task<List<Yahoo.Point>>> Chart, Func<Task<string>> Profile,
                       (string text, Action act)[] Actions);

    public static void Open(Form owner, Data d)
    {
        if (current is not { IsDisposed: false })
        {
            current = new PlayerCard();
            current.Show(owner);
        }
        current.Fill(d);
        current.Activate();
    }

    async void Fill(Data d)
    {
        int v = ++version;
        var r = d.Row;
        var a = Scout.Attributes(r, d.TtmYield, d.Vol);
        var (p, role, color) = Scout.Position(r, a);
        double st = Scout.Stars(r, a);

        Text = $"선수 카드 · {r.Q.Name}";
        name.Text = r.Q.Name;
        sub.Text = $"{r.Q.Code}  ·  {(r.Q.IsUs ? "미국" : r.Q.Market)}" + (r.Q.NameEn != null ? $"  ·  {r.Q.NameEn}" : "") + (r.Q.Sector != null ? $"  ·  {Yahoo.SectorKo(r.Q.Sector)}" : "");
        price.Text = r.Q.Price <= 0 ? "" : $"{Won.Price(r.Q.Price, r.Q.Currency)}  ({r.Q.ChangeRate:+0.00;-0.00;0.00}%)";
        price.ForeColor = r.Q.ChangeRate > 0 ? Theme.Up : r.Q.ChangeRate < 0 ? Theme.Down : Theme.Text;
        stars.Text = new string('★', (int)st) + (st % 1 >= 0.5 ? "½" : "") + new string('☆', 5 - (int)Math.Ceiling(st));
        int? ovr = Scout.Overall(a);
        overall.Text = (ovr is int o ? $"능력 {o}  ·  " : "") + (r.Total is double t ? $"종합 점수 {t:0}점" : "능력치 평균으로 매긴 별점");
        pos.Text = $"{p} · {role}";
        pos.Tint = color;
        pos.Size = pos.GetPreferredSize(Size.Empty);
        pos.Parent?.PerformLayout();
        pos.Left = (pos.Parent?.Width ?? 0) - pos.Width;
        attrs.SetData(a);

        actions.Controls.Clear();
        foreach (var (text, act) in d.Actions.Reverse())
        {
            var b = Theme.Secondary(text);
            b.Click += (_, _) => act();
            actions.Controls.Add(b);
        }

        string rep = Scout.Report(a, st);
        report.SetText(rep + "\n\n■ 회사 소개\n받는 중...");
        chart.SetMessage("주가를 받는 중...");
        try
        {
            var pts = await d.Chart();
            if (v == version) chart.SetData(pts, r.Q.IsUs);
        }
        catch (Exception ex) { if (v == version) chart.SetMessage("주가를 못 받았어요: " + ex.Message.Split('\n')[0]); }
        string prof;
        try { prof = await d.Profile(); } catch (Exception ex) { prof = "회사 정보를 못 받았어요: " + ex.Message.Split('\n')[0]; }
        if (v == version && !IsDisposed) report.SetText(rep + "\n\n■ 회사 소개\n" + prof);
    }
}

// 포메이션: 우리 팀 종목을 축구장에 포지션별로 세운다. 위가 공격, 아래가 수비, 맨 아래 골키퍼 자리는 비상금(투자 밖).
class Formation : Control
{
    public record Player(string Pos, string Name, double Weight, double Stars, Color Color);
    List<Player> players = new();
    string staff = "";

    public Formation()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.CardBg;
    }

    public void SetData(List<Player> p, string staffLine) { players = p; staff = staffLine; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        var pitch = new RectangleF(2, 22 * s, Width - 4, Height - 24 * s);
        if (pitch.Width < 40 * s || pitch.Height < 60 * s) return; // 크기가 0이면 그라데이션 붓에서 오류가 난다
        using (var path = Theme.Round(pitch, 12 * s))
        using (var grass = new LinearGradientBrush(pitch, Color.FromArgb(22, 78, 50), Color.FromArgb(14, 52, 34), 90f))
            g.FillPath(grass, path);
        // 잔디 줄무늬와 라인
        using (var stripe = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
            for (int i = 0; i < 8; i += 2) g.FillRectangle(stripe, pitch.X + 6 * s, pitch.Y + pitch.Height * i / 8, pitch.Width - 12 * s, pitch.Height / 8);
        using var line = new Pen(Color.FromArgb(70, 255, 255, 255), 1.2f * s);
        var inner = RectangleF.Inflate(pitch, -8 * s, -8 * s);
        g.DrawRectangle(line, inner.X, inner.Y, inner.Width, inner.Height);
        float midY = inner.Y + inner.Height / 2, cr = Math.Min(inner.Width, inner.Height) * 0.12f;
        g.DrawLine(line, inner.X, midY, inner.Right, midY);
        g.DrawEllipse(line, inner.X + inner.Width / 2 - cr, midY - cr, cr * 2, cr * 2);
        float boxW = inner.Width * 0.5f, boxH = inner.Height * 0.12f;
        g.DrawRectangle(line, inner.X + (inner.Width - boxW) / 2, inner.Y, boxW, boxH);
        g.DrawRectangle(line, inner.X + (inner.Width - boxW) / 2, inner.Bottom - boxH, boxW, boxH);

        TextRenderer.DrawText(g, staff, Theme.Small, new Rectangle(0, 0, Width, (int)(20 * s)), Theme.Muted, TextFormatFlags.EndEllipsis);
        if (players.Count == 0)
        {
            TextRenderer.DrawText(g, "수석코치를 고르면 선수들이 들어와요", Theme.Small, Rectangle.Round(pitch), Color.FromArgb(200, 255, 255, 255),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        // 줄: 공격 → 중원 → 수비 → 골키퍼(비상금)
        var rows = new[] { ("공격수", 0.17f), ("미드필더", 0.42f), ("수비수", 0.67f) };
        foreach (var (pos, fy) in rows)
        {
            var line_ = players.Where(p => p.Pos == pos).ToList();
            for (int i = 0; i < line_.Count; i++)
            {
                float cx = inner.X + inner.Width * (i + 1) / (line_.Count + 1), cy = inner.Y + inner.Height * fy;
                float r = Math.Min(20 * s, inner.Width / (line_.Count + 1) * 0.38f);
                DrawPlayer(g, line_[i], cx, cy, r, s);
            }
        }
        // 골키퍼 = 비상금 (투자 밖에 두는 돈)
        float gy = inner.Y + inner.Height * 0.88f, gr = 16 * s;
        using (var dash = new Pen(Color.FromArgb(160, 255, 255, 255), 1.4f * s) { DashStyle = DashStyle.Dash })
            g.DrawEllipse(dash, inner.X + inner.Width / 2 - gr, gy - gr, gr * 2, gr * 2);
        TextRenderer.DrawText(g, "GK", Theme.Small, Rectangle.Round(new RectangleF(inner.X + inner.Width / 2 - gr, gy - gr, gr * 2, gr * 2)), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "비상금 (생활비 3~6개월, 투자 밖)", Theme.Small, new Rectangle((int)inner.X, (int)(gy + gr), (int)inner.Width, (int)(18 * s)),
            Color.FromArgb(200, 255, 255, 255), TextFormatFlags.HorizontalCenter);
    }

    static void DrawPlayer(Graphics g, Player p, float cx, float cy, float r, float s)
    {
        using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(shadow, cx - r + 2, cy - r + 3, r * 2, r * 2);
        using (var fill = new SolidBrush(p.Color)) g.FillEllipse(fill, cx - r, cy - r, r * 2, r * 2);
        using (var ring = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f * s)) g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
        TextRenderer.DrawText(g, $"{p.Weight * 100:0}", Theme.Bold, Rectangle.Round(new RectangleF(cx - r, cy - r, r * 2, r * 2)), Color.FromArgb(15, 15, 15),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        string name = Names.Short(p.Name);
        var nameRect = new Rectangle((int)(cx - 60 * s), (int)(cy + r + 1), (int)(120 * s), (int)(17 * s));
        TextRenderer.DrawText(g, name, Theme.Small, nameRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        string stars = new string('★', (int)Math.Round(p.Stars));
        TextRenderer.DrawText(g, stars, Theme.Small, nameRect with { Y = nameRect.Bottom - (int)(2 * s) }, Color.FromArgb(250, 204, 21), TextFormatFlags.HorizontalCenter);
    }
}
