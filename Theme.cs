using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KrxScreen;

// 색·글꼴·공통 컨트롤 스타일 (블랙 테마)
static class Theme
{
    public static readonly Color Sidebar = Color.FromArgb(0, 0, 0);
    public static readonly Color SidebarHover = Color.FromArgb(24, 24, 27);
    public static readonly Color SidebarText = Color.FromArgb(161, 161, 170);
    public static readonly Color Bg = Color.FromArgb(9, 9, 11);
    public static readonly Color CardBg = Color.FromArgb(19, 19, 22);
    public static readonly Color InputBg = Color.FromArgb(28, 28, 32);
    public static readonly Color Border = Color.FromArgb(39, 39, 42);
    public static readonly Color Text = Color.FromArgb(244, 244, 245);
    public static readonly Color Muted = Color.FromArgb(140, 140, 150);
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);
    public static readonly Color AccentSoft = Color.FromArgb(30, 41, 70);
    public static readonly Color Good = Color.FromArgb(34, 197, 94);
    public static readonly Color Bad = Color.FromArgb(239, 68, 68);
    public static readonly Color Up = Color.FromArgb(248, 81, 73);    // 국내 관례: 상승 빨강
    public static readonly Color Down = Color.FromArgb(77, 141, 255); // 하락 파랑
    public static readonly Color Disabled = Color.FromArgb(52, 52, 58);
    public static readonly Color HeaderBg = Color.FromArgb(24, 24, 27);
    public static readonly Color AltRow = Color.FromArgb(23, 23, 26);
    // 채권 / 지수 / 배당 / 개별주
    public static readonly Color[] CatColors =
    {
        Color.FromArgb(96, 165, 250), Color.FromArgb(167, 139, 250), Color.FromArgb(52, 211, 153), Color.FromArgb(251, 191, 36),
    };

    const string Face = "맑은 고딕";
    public static readonly Font Body = new(Face, 9.5F);
    public static readonly Font Bold = new(Face, 9.5F, FontStyle.Bold);
    public static readonly Font Small = new(Face, 8.5F);
    public static readonly Font H1 = new(Face, 16F, FontStyle.Bold);
    public static readonly Font H2 = new(Face, 10.5F, FontStyle.Bold);
    public static readonly Font Nav = new(Face, 10.5F);
    public static readonly Font Kpi = new(Face, 15F, FontStyle.Bold);
    public static readonly Font Logo = new(Face, 14F, FontStyle.Bold);
    public static readonly Font Search = new(Face, 12F);
    public static readonly Font Icon = new(FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 12F);

    public static Label Text_(string text, Font font = null, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = font ?? Body, ForeColor = color ?? Text,
        Padding = new Padding(0, 7, 6, 0), BackColor = Color.Transparent,
    };

    static Button Btn(string text, Color bg, Color fg)
    {
        var b = new RoundButton
        {
            Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(0, 36), Padding = new Padding(18, 0, 18, 0),
            FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = Bold, Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0), UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = bg == InputBg ? 1 : 0;
        b.FlatAppearance.BorderColor = Border;
        b.EnabledChanged += (_, _) =>
        {
            b.BackColor = b.Enabled ? bg : Disabled;
            b.ForeColor = b.Enabled ? fg : Muted;
        };
        return b;
    }

    public static Button Primary(string text) => Btn(text, Accent, Color.White);
    public static Button Danger(string text) => Btn(text, Bad, Color.White);
    public static Button Secondary(string text) => Btn(text, InputBg, Text);

    // 입력칸·라디오 등 기본 컨트롤을 어두운 색으로
    public static void StyleInput(Control c)
    {
        c.BackColor = c is RadioButton or CheckBox ? Color.Transparent : InputBg;
        c.ForeColor = Text;
        if (c is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
        if (c is NumericUpDown n) n.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = CardBg;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = Border;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 34;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderBg, ForeColor = Muted, Font = Bold, SelectionBackColor = HeaderBg, SelectionForeColor = Muted,
            Padding = new Padding(4, 0, 4, 0),
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardBg, ForeColor = Text, Font = Body, SelectionBackColor = AccentSoft, SelectionForeColor = Text,
            Padding = new Padding(4, 0, 4, 0),
        };
        g.AlternatingRowsDefaultCellStyle.BackColor = AltRow;
        g.RowTemplate.Height = 30;
        g.RowHeadersVisible = false;
        // 스크롤 깜빡임 줄이기
        typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(g, true);
        DarkScrollbars(g);
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // DataGridView는 스크롤바가 자식 컨트롤이라 자식에도 적용
    public static void DarkScrollbars(Control c)
    {
        void Apply()
        {
            SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            foreach (Control ch in c.Controls) SetWindowTheme(ch.Handle, "DarkMode_Explorer", null);
        }
        if (c.IsHandleCreated) Apply();
        c.HandleCreated += (_, _) => Apply();
    }

    // Windows 10 1809+ / 11의 어두운 제목 표시줄
    public static void DarkTitleBar(Form f)
    {
        f.HandleCreated += (_, _) =>
        {
            int on = 1;
            if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
        };
    }

    public static Panel Spacer(int h) => new() { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color LabelColor(string label) => label switch
    {
        "고배당" or "초고배당" or "꾸준배당" => Color.FromArgb(52, 211, 153),
        "가치주" or "저PER" or "저PBR" => Color.FromArgb(96, 165, 250),
        "고ROE" or "성장주" or "가치 점수 우수" => Color.FromArgb(167, 139, 250),
        "재무건전" or "흑자 지속" => Color.FromArgb(45, 212, 191),
        "급등" => Up,
        "급락" => Down,
        "거래대금 상위" => Color.FromArgb(251, 191, 36),
        _ => Muted,
    };
}

// 모서리가 둥근 어두운 카드 + 1px 테두리. 제목이 있으면 위에 붙는다.
class Card : Panel
{
    public Card(string title = null, Control content = null)
    {
        BackColor = Theme.CardBg;
        Padding = new Padding(16, 12, 16, 14);
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 12, 12);
        DoubleBuffered = true;
        if (content != null) { content.Dock = DockStyle.Fill; Controls.Add(content); }
        if (title != null)
        {
            Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 });
            Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 24, Font = Theme.H2, ForeColor = Theme.Text });
        }
    }

    // 바깥 모서리는 부모 배경색으로 칠하고 그 위에 둥근 카드를 그린다 (안쪽 내용은 여백 안에 있어 모서리를 가리지 않는다)
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 14 * s);
        using (var fill = new SolidBrush(BackColor)) g.FillPath(fill, path);
        using var pen = new Pen(Theme.Border);
        g.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}

// 알약 모양 버튼. 마우스를 올리면 조금 밝아진다.
class RoundButton : Button
{
    bool hover;

    public RoundButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f);
        var bg = hover && Enabled ? ControlPaint.Light(BackColor, 0.15f) : BackColor;
        using (var fill = new SolidBrush(bg)) g.FillPath(fill, path);
        if (FlatAppearance.BorderSize > 0)
        {
            using var pen = new Pen(FlatAppearance.BorderColor);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// 요약 숫자 카드
class KpiTile : Card
{
    readonly Label value = new() { Dock = DockStyle.Top, Height = 34, Font = Theme.Kpi, ForeColor = Theme.Text, Text = "—" };
    readonly Label caption, note;

    public KpiTile(string caption)
    {
        Controls.Add(note = new Label { Dock = DockStyle.Top, Height = 20, Font = Theme.Small, ForeColor = Theme.Muted });
        Controls.Add(value);
        Controls.Add(this.caption = new Label { Text = caption, Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted });
    }

    public void Set(string text, Color? color = null, string note = null, string caption = null)
    {
        value.Text = text;
        value.ForeColor = color ?? Theme.Text;
        if (note != null) this.note.Text = note;
        if (caption != null) this.caption.Text = caption;
    }
}

// 둥근 알약 모양 라벨/칩. Selectable이면 클릭으로 켜고 끈다.
class Pill : Control
{
    public Color Tint = Theme.Muted;
    public bool Selected;
    bool hover;

    public Pill(string text, Color? tint = null, bool clickable = false)
    {
        Text = text;
        Font = Theme.Small;
        if (tint != null) Tint = tint.Value;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 6, 6);
        if (clickable)
        {
            Cursor = Cursors.Hand;
            MouseEnter += (_, _) => { hover = true; Invalidate(); };
            MouseLeave += (_, _) => { hover = false; Invalidate(); };
        }
        Size = GetPreferredSize(Size.Empty);
    }

    public override Size GetPreferredSize(Size proposed)
    {
        var t = TextRenderer.MeasureText(Text, Font);
        float s = DeviceDpi / 96f;
        return new Size(t.Width + (int)(16 * s), t.Height + (int)(8 * s));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Size = GetPreferredSize(Size.Empty); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, Height / 2f);
        int alpha = Selected ? 90 : hover ? 60 : 32;
        using (var fill = new SolidBrush(Color.FromArgb(alpha, Tint))) g.FillPath(fill, path);
        using (var pen = new Pen(Color.FromArgb(Selected ? 220 : 110, Tint))) g.DrawPath(pen, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Selected ? Color.White : Color.FromArgb(235, Tint),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// 사이드바 메뉴 한 줄: 아이콘 + 제목
class NavItem : Control
{
    public readonly string Glyph;
    bool active, hover;

    public NavItem(string glyph, string title)
    {
        Glyph = glyph;
        Text = title;
        Height = 46;
        Dock = DockStyle.Top;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
    }

    public bool Active { get => active; set { active = value; Invalidate(); } }

    // 화면 읽기 프로그램·UI 자동화에서 버튼으로 보이고 누를 수 있게
    protected override AccessibleObject CreateAccessibilityInstance() => new NavAccessible(this);

    class NavAccessible : ControlAccessibleObject
    {
        readonly NavItem item;
        public NavAccessible(NavItem item) : base(item) => this.item = item;
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string Name => item.Text;
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => item.InvokeOnClick(item, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Theme.Sidebar);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (active || hover)
        {
            using var path = Theme.Round(new RectangleF(10 * s, 3 * s, Width - 20 * s, Height - 6 * s), 12 * s);
            using var b = new SolidBrush(active ? Theme.AccentSoft : Theme.SidebarHover);
            g.FillPath(b, path);
        }
        if (active)
        {
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, 10 * s, Height / 2f - 9 * s, 3 * s, 18 * s);
        }
        var fg = active ? Color.White : Theme.SidebarText;
        TextRenderer.DrawText(g, Glyph, Theme.Icon, new Rectangle((int)(24 * s), 0, (int)(28 * s), Height), fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, Text, active ? Theme.H2 : Theme.Nav, new Rectangle((int)(60 * s), 0, Width, Height), fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}

// 시세 선 그래프. 기간 등락에 따라 빨강(상승)/파랑(하락), 마우스를 올리면 그날 종가.
class PriceChart : Control
{
    List<Yahoo.Point> data;
    string message = "종목을 선택하세요";
    bool usd;
    int hover = -1;

    public PriceChart()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.CardBg;
        Dock = DockStyle.Top;
        Height = 170;
        MouseMove += (_, e) =>
        {
            if (data == null || data.Count < 2) return;
            int i = (int)Math.Round((e.X - Pad) / (double)(Width - Pad * 2) * (data.Count - 1));
            i = Math.Clamp(i, 0, data.Count - 1);
            if (i != hover) { hover = i; Invalidate(); }
        };
        MouseLeave += (_, _) => { hover = -1; Invalidate(); };
    }

    float Pad => 4 * DeviceDpi / 96f;

    public void SetData(List<Yahoo.Point> d, bool isUsd) { data = d; usd = isUsd; message = d == null || d.Count < 2 ? "시세 기록이 없어요" : null; hover = -1; Invalidate(); }
    public void SetMessage(string m) { data = null; message = m; Invalidate(); }

    string Money(double v) => usd ? $"${v:N2}" : $"{v:N0}원";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (message != null)
        {
            TextRenderer.DrawText(g, message, Theme.Small, ClientRectangle, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        float s = DeviceDpi / 96f, top = 22 * s, bottom = Height - 18 * s, left = Pad, right = Width - Pad;
        double min = data.Min(p => p.Close), max = data.Max(p => p.Close);
        if (max == min) max = min + 1;
        PointF P(int i) => new(left + (right - left) * i / (data.Count - 1), bottom - (float)((data[i].Close - min) / (max - min)) * (bottom - top));

        double first = data[0].Close, last = data[^1].Close, chg = (last / first - 1) * 100;
        var color = chg >= 0 ? Theme.Up : Theme.Down;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var pts = Enumerable.Range(0, data.Count).Select(P).ToArray();
        using (var area = new GraphicsPath())
        {
            area.AddLines(pts);
            area.AddLine(pts[^1], new PointF(right, bottom));
            area.AddLine(new PointF(right, bottom), new PointF(left, bottom));
            area.CloseFigure();
            using var fill = new LinearGradientBrush(new RectangleF(0, top, Width, bottom - top + 1), Color.FromArgb(70, color), Color.FromArgb(0, color), 90f);
            g.FillPath(fill, area);
        }
        using (var pen = new Pen(color, 1.8f * s)) g.DrawLines(pen, pts);

        // 기간 등락률(왼쪽 위), 최고·최저(오른쪽 위), 시작·끝 날짜(아래)
        TextRenderer.DrawText(g, $"{chg:+0.00;-0.00;0.00}%", Theme.Bold, new Point((int)left, 0), color);
        TextRenderer.DrawText(g, $"최고 {Money(max)}  ·  최저 {Money(min)}", Theme.Small,
            new Rectangle(0, (int)(2 * s), (int)right, (int)(18 * s)), Theme.Muted, TextFormatFlags.Right);
        TextRenderer.DrawText(g, data[0].Date.ToString("yy.MM.dd"), Theme.Small, new Point((int)left, (int)(bottom + 2 * s)), Theme.Muted);
        TextRenderer.DrawText(g, data[^1].Date.ToString("yy.MM.dd"), Theme.Small,
            new Rectangle(0, (int)(bottom + 2 * s), (int)right, (int)(16 * s)), Theme.Muted, TextFormatFlags.Right);

        if (hover >= 0)
        {
            var hp = P(hover);
            using var line = new Pen(Color.FromArgb(120, Theme.Text)) { DashStyle = DashStyle.Dot };
            g.DrawLine(line, hp.X, top, hp.X, bottom);
            using var dot = new SolidBrush(color);
            g.FillEllipse(dot, hp.X - 4 * s, hp.Y - 4 * s, 8 * s, 8 * s);
            string tip = $"{data[hover].Date:yyyy.MM.dd}  {Money(data[hover].Close)}";
            var size = TextRenderer.MeasureText(tip, Theme.Small);
            float tx = Math.Clamp(hp.X - size.Width / 2f, 0, Width - size.Width), ty = top;
            using var bg = new SolidBrush(Theme.InputBg);
            g.FillRectangle(bg, tx - 4, ty - 2, size.Width + 8, size.Height + 4);
            TextRenderer.DrawText(g, tip, Theme.Small, new Point((int)tx, (int)ty), Theme.Text);
        }
    }
}

// 시뮬레이터: 평가금액(선+채움)과 넣은 원금(점선). 마우스를 올리면 그 달 값.
class SimChart : Control
{
    List<Backtest.Point> pts;
    int hover = -1;

    public SimChart()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.CardBg;
        MouseMove += (_, e) =>
        {
            if (pts == null || pts.Count < 2) return;
            int i = Math.Clamp((int)Math.Round((e.X - 8.0) / Math.Max(1, Width - 16) * (pts.Count - 1)), 0, pts.Count - 1);
            if (i != hover) { hover = i; Invalidate(); }
        };
        MouseLeave += (_, _) => { hover = -1; Invalidate(); };
    }

    public void SetData(List<Backtest.Point> p) { pts = p; hover = -1; Invalidate(); }

    static string Won(double v) => v >= 1e8 ? $"{v / 1e8:0.00}억" : $"{v / 1e4:N0}만";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (pts == null || pts.Count < 2)
        {
            TextRenderer.DrawText(g, "구성을 넣고 [시뮬레이션 실행]을 누르세요", Theme.Small, ClientRectangle, Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        float s = DeviceDpi / 96f, top = 22 * s, bottom = Height - 20 * s, left = 8, right = Width - 8;
        double max = pts.Max(p => Math.Max(p.Value + p.DivCash, p.Principal)) * 1.05, min = 0;
        PointF P(int i, double v) => new(left + (right - left) * i / (pts.Count - 1), bottom - (float)((v - min) / (max - min)) * (bottom - top));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var val = Enumerable.Range(0, pts.Count).Select(i => P(i, pts[i].Value + pts[i].DivCash)).ToArray();
        var color = pts[^1].Value + pts[^1].DivCash >= pts[^1].Principal ? Theme.Up : Theme.Down;
        using (var area = new GraphicsPath())
        {
            area.AddLines(val);
            area.AddLine(val[^1], new PointF(right, bottom));
            area.AddLine(new PointF(right, bottom), new PointF(left, bottom));
            area.CloseFigure();
            using var fill = new LinearGradientBrush(new RectangleF(0, top, Width, bottom - top + 1), Color.FromArgb(60, color), Color.FromArgb(0, color), 90f);
            g.FillPath(fill, area);
        }
        using (var pen = new Pen(color, 2f * s)) g.DrawLines(pen, val);
        using (var dash = new Pen(Theme.Muted, 1.5f * s) { DashStyle = DashStyle.Dash })
            g.DrawLines(dash, Enumerable.Range(0, pts.Count).Select(i => P(i, pts[i].Principal)).ToArray());

        TextRenderer.DrawText(g, "━ 평가금액(받은 배당 포함)   ┅ 넣은 원금", Theme.Small, new Point(0, 0), Theme.Muted);
        TextRenderer.DrawText(g, pts[0].Month.ToString("yyyy.MM"), Theme.Small, new Point((int)left, (int)(bottom + 2 * s)), Theme.Muted);
        TextRenderer.DrawText(g, pts[^1].Month.ToString("yyyy.MM"), Theme.Small, new Rectangle(0, (int)(bottom + 2 * s), (int)right, (int)(16 * s)), Theme.Muted, TextFormatFlags.Right);
        TextRenderer.DrawText(g, Won(max / 1.05), Theme.Small, new Rectangle(0, 0, (int)right, (int)(16 * s)), Theme.Muted, TextFormatFlags.Right);

        if (hover >= 0)
        {
            var hp = P(hover, pts[hover].Value + pts[hover].DivCash);
            using var line = new Pen(Color.FromArgb(120, Theme.Text)) { DashStyle = DashStyle.Dot };
            g.DrawLine(line, hp.X, top, hp.X, bottom);
            using var dot = new SolidBrush(color);
            g.FillEllipse(dot, hp.X - 4 * s, hp.Y - 4 * s, 8 * s, 8 * s);
            var p = pts[hover];
            string tip = $"{p.Month:yyyy.MM}  평가 {Won(p.Value + p.DivCash)}원 · 원금 {Won(p.Principal)}원 · {(p.Value + p.DivCash) / Math.Max(1, p.Principal) * 100 - 100:+0.0;-0.0}%";
            var size = TextRenderer.MeasureText(tip, Theme.Small);
            float tx = Math.Clamp(hp.X - size.Width / 2f, 0, Width - size.Width);
            using var bg = new SolidBrush(Theme.InputBg);
            g.FillRectangle(bg, tx - 4, top - 2, size.Width + 8, size.Height + 4);
            TextRenderer.DrawText(g, tip, Theme.Small, new Point((int)tx, (int)top), Theme.Text);
        }
    }
}

// 연도별 막대 (순이익·배당 추이). 음수는 빨강.
class BarSeries : Control
{
    Dictionary<int, double> data;
    readonly string unit;
    readonly double divisor;
    readonly Color color;

    public BarSeries(Color color, string unit, double divisor)
    {
        this.color = color;
        this.unit = unit;
        this.divisor = divisor;
        DoubleBuffered = true;
        BackColor = Theme.CardBg;
        Dock = DockStyle.Top;
        Height = 120;
    }

    public void SetData(Dictionary<int, double> d) { data = d; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(BackColor);
        if (data == null || data.Count == 0)
        {
            TextRenderer.DrawText(g, "국내 가치 분석 종목만 있어요", Theme.Small, ClientRectangle, Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var years = data.Keys.OrderBy(y => y).ToList();
        double max = Math.Max(data.Values.Max(), 0), min = Math.Min(data.Values.Min(), 0);
        if (max == min) max = min + 1;
        float top = 14 * s, bottom = Height - 18 * s, h = bottom - top;
        float zeroY = top + (float)(max / (max - min)) * h;
        float slot = (float)Width / years.Count, bw = Math.Max(3, slot * 0.6f);
        using var axis = new Pen(Theme.Border);
        g.DrawLine(axis, 0, zeroY, Width, zeroY);
        for (int i = 0; i < years.Count; i++)
        {
            double v = data[years[i]];
            float x = i * slot + (slot - bw) / 2, y = zeroY - (float)(v / (max - min)) * h;
            using var b = new SolidBrush(v < 0 ? Theme.Bad : color);
            g.FillRectangle(b, x, Math.Min(y, zeroY), bw, Math.Max(1, Math.Abs(zeroY - y)));
            if (i == 0 || i == years.Count - 1 || years.Count <= 6)
                TextRenderer.DrawText(g, $"'{years[i] % 100:00}", Theme.Small, new Rectangle((int)(i * slot), (int)bottom, (int)slot, (int)(18 * s)),
                    Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
        double last = data[years[^1]] / divisor;
        TextRenderer.DrawText(g, $"{years[^1]}: {last:N0}{unit}", Theme.Small, new Rectangle(0, 0, Width, (int)(14 * s)), Theme.Muted, TextFormatFlags.Right);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}

// 분류별 현재 비중 vs 목표 비중 막대. 회색 띠 = 목표 ±5%p 허용 범위, 흰 선 = 목표.
class WeightChart : Control
{
    double[] current;

    public WeightChart()
    {
        DoubleBuffered = true;
        BackColor = Theme.CardBg;
        Height = 150;
        Dock = DockStyle.Top;
    }

    public void SetData(double[] currentPct) { current = currentPct; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        int n = Portfolio.Cats.Length, rowH = Math.Max((int)(24 * s), (Height - 8) / n);
        int labelW = (int)(48 * s), valueW = (int)(150 * s), barX = labelW, barW = Math.Max(40, Width - labelW - valueW - 8);
        const double scale = 60; // 막대 끝 = 60%
        using var text = new SolidBrush(Theme.Text);
        using var band = new SolidBrush(Color.FromArgb(52, 52, 58));
        using var track = new SolidBrush(Theme.InputBg);
        using var tick = new Pen(Theme.Text, 2);

        for (int i = 0; i < n; i++)
        {
            int y = 4 + i * rowH;
            float bh = 12 * s, mid = y + rowH / 2f, barY = mid - bh / 2;
            double target = Portfolio.Target[i] * 100;
            float X(double pct) => barX + (float)(Math.Clamp(pct, 0, scale) / scale * barW);
            float textY = mid - Theme.Bold.GetHeight(g) / 2;

            g.DrawString(Portfolio.Cats[i], Theme.Bold, text, 0, textY);
            g.FillRectangle(track, barX, barY, barW, bh);
            g.FillRectangle(band, X(target - Portfolio.Band * 100), barY, X(target + Portfolio.Band * 100) - X(target - Portfolio.Band * 100), bh);
            if (current != null)
            {
                using var fill = new SolidBrush(Theme.CatColors[i]);
                g.FillRectangle(fill, barX, barY + bh / 4, X(current[i]) - barX, bh / 2);
            }
            g.DrawLine(tick, X(target), barY - bh / 4, X(target), barY + bh * 1.25f);

            string label = current == null ? $"목표 {target:0}%" : $"{current[i]:0.0}%  (목표 {target:0}%)";
            bool off = current != null && Math.Abs(current[i] - target) > Portfolio.Band * 100;
            using var vb = new SolidBrush(off ? Theme.Bad : Theme.Muted);
            g.DrawString(label, off ? Theme.Bold : Theme.Body, vb, barX + barW + 10 * s, textY);
        }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}

// 오른쪽 클릭 메뉴 등을 어두운 색으로
class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.CardBg;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Theme.AccentSoft;
    public override Color MenuItemSelected => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientBegin => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientEnd => Theme.AccentSoft;
    public override Color SeparatorDark => Theme.Border;
    public override Color ImageMarginGradientBegin => Theme.CardBg;
    public override Color ImageMarginGradientMiddle => Theme.CardBg;
    public override Color ImageMarginGradientEnd => Theme.CardBg;
}
