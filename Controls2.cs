using System.Drawing.Drawing2D;

namespace KrxScreen;

// 사이드바 묶음 제목 (운영 · 시장 · 도움)
class NavSection : Control
{
    public NavSection(string title)
    {
        Text = title;
        Height = 30;
        Dock = DockStyle.Top;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        e.Graphics.Clear(Theme.Sidebar);
        TextRenderer.DrawText(e.Graphics, Text, Theme.Small, new Rectangle((int)(26 * s), (int)(8 * s), Width, Height - (int)(8 * s)), Theme.SidebarSection,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

// 화면 위 탭: 글자 + 선택되면 아래 강조 막대. 같은 메뉴 안의 화면을 오간다.
class TabItem : Control
{
    bool selected, hover;

    public TabItem(string text)
    {
        Text = text;
        Font = Theme.H2;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 4, 0);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
        Size = GetPreferredSize(Size.Empty);
    }

    public bool Selected { get => selected; set { selected = value; Invalidate(); } }

    public override Size GetPreferredSize(Size proposed)
    {
        var t = TextRenderer.MeasureText(Text, Font);
        float s = DeviceDpi / 96f;
        return new Size(t.Width + (int)(28 * s), t.Height + (int)(18 * s));
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TabAccessible(this);

    class TabAccessible : ControlAccessibleObject
    {
        readonly TabItem tab;
        public TabAccessible(TabItem tab) : base(tab) => this.tab = tab;
        public override AccessibleRole Role => AccessibleRole.PageTab;
        public override string Name => tab.Text;
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => tab.InvokeOnClick(tab, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (hover && !selected)
            using (var p = Theme.Round(new RectangleF(2 * s, 2 * s, Width - 4 * s, Height - 8 * s), 8 * s))
            using (var b = new SolidBrush(Theme.SidebarHover)) g.FillPath(b, p);
        var fg = selected ? Theme.Text : hover ? Theme.Text : Theme.Muted;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - (int)(4 * s)), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (selected)
            using (var p = Theme.Round(new RectangleF(10 * s, Height - 4 * s, Width - 20 * s, 3 * s), 1.5f * s))
            using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
    }
}

// 사이드바 아래 '내 정보' 카드: 순자산·시즌 등급·연봉을 늘 보여 주고, 누르면 내 정보(재무 설정)를 연다
class ProfileCard : Control
{
    string line1 = "게임을 시작하면 순자산이 보여요", line2 = "눌러서 내 정보 설정", grade = "";
    bool hover;

    public ProfileCard()
    {
        Dock = DockStyle.Bottom;
        Height = 76;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
    }

    public void Set(string l1, string l2, string seasonGrade) { line1 = l1; line2 = l2; grade = seasonGrade ?? ""; Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance() => new ProfileAccessible(this);

    class ProfileAccessible : ControlAccessibleObject
    {
        readonly ProfileCard c;
        public ProfileAccessible(ProfileCard c) : base(c) => this.c = c;
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string Name => "내 정보";
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => c.InvokeOnClick(c, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Theme.Sidebar);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new RectangleF(10 * s, 6 * s, Width - 20 * s, Height - 12 * s);
        if (box.Width < 40 * s) return;
        using (var p = Theme.Round(box, 12 * s))
        {
            using (var b = new SolidBrush(hover ? Theme.SidebarHover : Theme.SidebarCard)) g.FillPath(b, p);
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        // 동그라미 아바타
        float d = 34 * s, ax = box.X + 10 * s, ay = box.Y + (box.Height - d) / 2;
        using (var lg = new LinearGradientBrush(new RectangleF(ax, ay, d, d), Theme.Accent, Theme.Gold, 45f)) g.FillEllipse(lg, ax, ay, d, d);
        TextRenderer.DrawText(g, "나", Theme.Bold, Rectangle.Round(new RectangleF(ax, ay, d, d)), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        float tx = ax + d + 10 * s, tw = box.Right - tx - (grade != "" ? 34 * s : 8 * s);
        TextRenderer.DrawText(g, "내 정보", Theme.Bold, new Rectangle((int)tx, (int)(box.Y + 7 * s), (int)tw, (int)(20 * s)), Theme.Text, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, line1, Theme.Small, new Rectangle((int)tx, (int)(box.Y + 27 * s), (int)tw, (int)(17 * s)), Theme.Text, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, line2, Theme.Small, new Rectangle((int)tx, (int)(box.Y + 44 * s), (int)tw, (int)(17 * s)), Theme.Muted, TextFormatFlags.EndEllipsis);
        if (grade != "")
        {
            var gb = new RectangleF(box.Right - 30 * s, box.Y + (box.Height - 22 * s) / 2, 22 * s, 22 * s);
            using (var p = Theme.Round(gb, 6 * s)) using (var b = new SolidBrush(Theme.GradeColor(grade))) g.FillPath(b, p);
            TextRenderer.DrawText(g, grade, Theme.Bold, Rectangle.Round(gb), Color.FromArgb(15, 15, 20), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
