using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KrxScreen;

// 색·글꼴·공통 컨트롤 스타일 — "미드나잇 레저": 남색 바탕, 누를 수 있는 곳은 남보라, 돈·보상은 금색.
// 가격 방향(상승 빨강·하락 하늘)과 좋고 나쁨(초록·주황)은 서로 다른 색이라 헷갈리지 않는다.
// 글자 대비는 모두 WCAG AA 이상 (본문 4.5:1). TextRenderer는 투명도를 무시하므로 글자 색은 불투명 색만 쓴다.
static class Theme
{
    static Color H(int rgb) => Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    // ---- 바탕 (어두운 순) ----
    public static readonly Color Sidebar = H(0x090E1A);      // 사이드바 (가장 어두움)
    public static readonly Color SidebarHover = H(0x141C2E);
    public static readonly Color SidebarText = H(0xA3AEC4);  // 8.6:1
    public static readonly Color SidebarSection = H(0x7C89A3); // 사이드바 묶음 제목 5.5:1
    public static readonly Color SidebarCard = H(0x111829);  // 사이드바 '내 정보' 카드
    public static readonly Color Bg = H(0x0C1220);           // 페이지
    public static readonly Color CardBg = H(0x131A2B);       // 카드·표·그래프 (자식이 같은 색으로 지우므로 단색)
    public static readonly Color CardTop = H(0x172035);      // 요약 숫자 칸 위쪽 그라데이션
    public static readonly Color InputBg = H(0x1B2439);      // 입력칸·보조 버튼·막대 바탕·말풍선
    public static readonly Color HeaderBg = H(0x182136);     // 표 머리글
    public static readonly Color AltRow = H(0x161D30);       // 표 줄무늬
    public static readonly Color Border = H(0x253049);       // 카드 테두리 (장식용)
    public static readonly Color BorderHi = H(0x36435F);     // 마우스 올림 테두리, 허용 범위 띠
    public static readonly Color GridLine = H(0x1E273D);     // 표 줄, 그래프 눈금, 구분선
    public static readonly Color InputBorder = H(0x5B6885);  // 입력칸 경계 (3:1)

    // ---- 글자 ----
    public static readonly Color Text = H(0xE8ECF4);         // 14.7:1 (카드 위)
    public static readonly Color TextSoft = H(0xBDC6D7);     // 보조 본문, 선택 안 된 탭 10:1
    public static readonly Color Muted = H(0x8E9BB4);        // 설명·머리글 6.2:1
    public static readonly Color DisabledText = H(0x6B7790);
    public static readonly Color Ink = H(0x0C1220);          // 밝은 바탕 위 어두운 글자 (능력치 숫자, 선수 원)

    // ---- 행동 (남보라) ----
    public static readonly Color Accent = H(0x4F5BE6);       // 주 버튼 (흰 글자 5.3:1)
    public static readonly Color AccentHover = H(0x5A66EE);
    public static readonly Color AccentPressed = H(0x4651D6);
    public static readonly Color AccentSoft = H(0x1F2752);   // 선택된 메뉴·줄
    public static readonly Color AccentText = H(0xA7AFFF);   // 글자·선으로 쓰는 강조 (링크, 탭 밑줄, 포커스)

    // ---- 의미 ----
    public static readonly Color Good = H(0x3FCB99);         // 좋음·배당·수입
    public static readonly Color GoodSoft = H(0x11302A);
    public static readonly Color Bad = H(0xFF8F45);          // 오류·손실·경고 — 일부러 빨강이 아님 (빨강은 상승)
    public static readonly Color BadSoft = H(0x38231A);
    public static readonly Color Up = H(0xFF6068);           // 국내 관례: 상승 빨강
    public static readonly Color UpSoft = H(0x3A1B24);
    public static readonly Color Down = H(0x47A6FF);         // 하락 하늘
    public static readonly Color DownSoft = H(0x132A4A);
    public static readonly Color Disabled = H(0x232C42);
    public static readonly Color Teal = H(0x5CC3D6);

    // ---- 돈·보상 (금색은 여기에만) ----
    public static readonly Color Gold = H(0xD9B45F);         // 별점·레벨·S등급·돈 숫자 칸 띠
    public static readonly Color GoldSoft = H(0x2D2717);
    public static readonly Color GoldLight = H(0xE8C976);
    public static readonly Color GoldDeep = H(0xB8903F);

    // ---- 축구장 (밤 경기장) ----
    public static readonly Color PitchTop = H(0x0F3631), PitchBottom = H(0x0B2623), PitchText = H(0xDCE3E8);

    // 채권 / 지수 / 배당 / 개별주
    public static readonly Color[] CatColors = { H(0x7CA4E2), H(0xA694F4), H(0x45C9A0), H(0xE6A25C) };

    // 능력치 1~20 단계 색: 낮음 주황(빨강 아님 — 빨강은 '오름'으로 읽힌다) → 보통 회색 → 좋음 연두 → 최고 초록
    public static readonly Color[] Attr = { H(0xFF8F45), H(0x8E9BB4), H(0xA3D36C), H(0x3FCB99) };

    // 시즌 등급 색 (S 금, A 초록, B 남보라, C 회색, D·F 주황 — 빨강은 상승이라 쓰지 않는다)
    public static Color GradeColor(string g) => g switch
    {
        "S" => Gold, "A" => Good, "B" => AccentText, "C" => TextSoft, _ => Bad,
    };

    // 두 색 섞기 (불투명 글자 색 만들 때)
    public static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    // ---- 글꼴: 맑은 고딕 (숫자 폭이 같아 표가 흔들리지 않는다). 크기는 96dpi에서 정수 픽셀 ----
    const string Face = "맑은 고딕";
    public static readonly Font Body = new(Face, 9.75F);
    public static readonly Font Bold = new(Face, 9.75F, FontStyle.Bold);
    public static readonly Font Small = new(Face, 9F);
    public static readonly Font SmallBold = new(Face, 9F, FontStyle.Bold);
    public static readonly Font H1 = new(Face, 16.5F, FontStyle.Bold);
    public static readonly Font H2 = new(Face, 10.5F, FontStyle.Bold);
    public static readonly Font Nav = new(Face, 10.5F);
    public static readonly Font NavBold = new(Face, 10.5F, FontStyle.Bold);
    public static readonly Font Kpi = new(Face, 16.5F, FontStyle.Bold);
    public static readonly Font KpiSmall = new(Face, 13.5F, FontStyle.Bold); // 칸이 좁을 때
    public static readonly Font Logo = new(Face, 13.5F, FontStyle.Bold);
    public static readonly Font Wordmark = new(FontFamily.Families.Any(f => f.Name == "Segoe UI Semibold") ? "Segoe UI Semibold" : "Segoe UI", 8.25F);
    public static readonly Font Search = new(Face, 12F);
    public static readonly Font Icon = new(FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 12F);

    public static Label Text_(string text, Font font = null, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = font ?? Body, ForeColor = color ?? Text,
        Padding = new Padding(0, 7, 6, 0), BackColor = Color.Transparent,
    };

    static Button Btn(string text, RoundButton.Kinds kind) => new RoundButton
    {
        Text = text, Kind = kind, AutoSize = true, Height = 36, MinimumSize = new Size(0, 36), Padding = new Padding(18, 0, 18, 0),
        FlatStyle = FlatStyle.Flat, Font = Bold, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), UseVisualStyleBackColor = false,
        BackColor = kind == RoundButton.Kinds.Primary ? Accent : InputBg, ForeColor = kind == RoundButton.Kinds.Primary ? Color.White : Text,
    };

    public static Button Primary(string text) => Btn(text, RoundButton.Kinds.Primary);   // 할 일 (남보라)
    public static Button Danger(string text) => Btn(text, RoundButton.Kinds.Danger);     // 되돌리기 어려운 일 (주황 테두리)
    public static Button Secondary(string text) => Btn(text, RoundButton.Kinds.Secondary);
    public static Button Reward(string text) => Btn(text, RoundButton.Kinds.Reward);     // 보상·결산 (금색)

    // 입력칸·라디오 등 기본 컨트롤을 어두운 색으로
    public static void StyleInput(Control c)
    {
        c.BackColor = c is RadioButton or CheckBox ? Color.Transparent : InputBg;
        c.ForeColor = Text;
        if (c is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
        if (c is NumericUpDown n) n.BorderStyle = BorderStyle.FixedSingle;
        if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat; // 밝은 기본 모양 대신 어두운 평면
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = CardBg;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = GridLine;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 34;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderBg, ForeColor = Muted, Font = SmallBold, SelectionBackColor = HeaderBg, SelectionForeColor = Muted,
            Padding = new Padding(8, 0, 8, 0),
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardBg, ForeColor = Text, Font = Body, SelectionBackColor = AccentSoft, SelectionForeColor = Text,
            Padding = new Padding(8, 0, 8, 0),
        };
        g.AlternatingRowsDefaultCellStyle.BackColor = AltRow;
        g.RowTemplate.Height = 32;
        g.RowHeadersVisible = false;
        // 고른 줄은 왼쪽에 남보라 막대 (채운 색만으로는 잘 안 보인다)
        g.RowPostPaint += (_, e) =>
        {
            if (!e.State.HasFlag(DataGridViewElementStates.Selected)) return;
            float sc = g.DeviceDpi / 96f;
            using var b = new SolidBrush(AccentText);
            e.Graphics.FillRectangle(b, e.RowBounds.X, e.RowBounds.Y + 4 * sc, 3 * sc, e.RowBounds.Height - 8 * sc);
        };
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

    // 앱 아이콘 (Assets/app.ico를 리소스로 넣어 둠). 여러 크기가 들어 있어 작업표시줄·제목 표시줄이 배율에 맞는 크기를 고른다.
    static Icon appIcon;
    public static Icon AppIcon
    {
        get
        {
            if (appIcon != null) return appIcon;
            try
            {
                using var st = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
                if (st != null) appIcon = new Icon(st);
            }
            catch { } // 아이콘이 없어도 앱은 뜬다
            return appIcon;
        }
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

    // 그래프 공통: 25·50·75% 점선 눈금, 마지막 점(카드색 테두리), 둥근 말풍선
    public static void GuideLines(Graphics g, float left, float right, float top, float bottom, float s)
    {
        using var pen = new Pen(GridLine, 1) { DashStyle = DashStyle.Dash };
        foreach (var f in new[] { 0.25f, 0.5f, 0.75f }) g.DrawLine(pen, left, top + (bottom - top) * f, right, top + (bottom - top) * f);
    }

    public static void EndDot(Graphics g, PointF p, Color c, float s)
    {
        float r = 4 * s;
        using (var ring = new SolidBrush(CardBg)) g.FillEllipse(ring, p.X - r - 2 * s, p.Y - r - 2 * s, (r + 2 * s) * 2, (r + 2 * s) * 2);
        using (var dot = new SolidBrush(c)) g.FillEllipse(dot, p.X - r, p.Y - r, r * 2, r * 2);
    }

    public static void Tooltip(Graphics g, RectangleF r, float s)
    {
        using var p = Round(r, 8 * s);
        using (var b = new SolidBrush(InputBg)) g.FillPath(b, p);
        using (var pen = new Pen(BorderHi)) g.DrawPath(pen, p);
    }

    public static Color LabelColor(string label) => label switch
    {
        "고배당" or "초고배당" or "꾸준배당" => Good,
        "가치주" or "저PER" or "저PBR" => CatColors[0],
        "고ROE" or "성장주" or "가치 점수 우수" => CatColors[1],
        "재무건전" or "흑자 지속" => Teal,
        "급등" => Up,
        "급락" => Down,
        "거래대금 상위" => Gold,
        _ => Muted,
    };
}

// 둥근 카드: 아래로 옅은 그림자, 1px 테두리, 위쪽 안쪽에 아주 옅은 빛. 제목이 있으면 제목 아래 구분선.
// 몸통은 단색이어야 한다 (표·그래프가 같은 색으로 자기 영역을 지우므로 그라데이션이면 이음매가 보인다).
class Card : Panel
{
    readonly Label title;

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
            Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent });
            Controls.Add(this.title = new Label { Text = title, Dock = DockStyle.Top, Height = 24, Font = Theme.H2, ForeColor = Theme.Text, BackColor = Color.Transparent, AutoEllipsis = true });
        }
    }

    protected GraphicsPath Body(float s) => Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 3.5f * s), 12 * s);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        if (Width < 24 * s || Height < 24 * s) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Body(s);
        // 그림자 두 겹
        using (var sh = (GraphicsPath)path.Clone())
        {
            using var m = new Matrix(); m.Translate(0, 2 * s); sh.Transform(m);
            using (var b = new SolidBrush(Color.FromArgb(50, 2, 5, 12))) g.FillPath(b, sh);
            using var m1 = new Matrix(); m1.Translate(0, -1 * s); sh.Transform(m1);
            using (var b = new SolidBrush(Color.FromArgb(90, 2, 5, 12))) g.FillPath(b, sh);
        }
        using (var fill = new SolidBrush(BackColor)) g.FillPath(fill, path);
        PaintBody(g, path, s);
        using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
        // 위쪽 안쪽의 옅은 빛
        using (var hi = new Pen(Color.FromArgb(18, 255, 255, 255))) g.DrawLine(hi, 12 * s, 1.5f, Width - 12 * s, 1.5f);
        if (title != null && title.Visible)
        {
            float y = title.Bottom + 3 * s;
            using var div = new Pen(Theme.GridLine);
            g.DrawLine(div, Padding.Left, y, Width - Padding.Right, y);
        }
    }

    // 몸통 위에 더 그릴 것 (요약 숫자 칸의 그라데이션·띠)
    protected virtual void PaintBody(Graphics g, GraphicsPath path, float s) { }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}

// 버튼: 둥근 사각형 (고르는 칩·탭은 알약, 하는 버튼은 사각형이라 구별된다). 상태마다 정해진 색 — 밝게 하면 흰 글자 대비가 떨어진다.
class RoundButton : Button
{
    public enum Kinds { Primary, Secondary, Danger, Reward }
    public Kinds Kind = Kinds.Secondary;
    bool hover, pressed;

    public RoundButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = pressed = false; Invalidate(); };
        MouseDown += (_, _) => { pressed = true; Invalidate(); };
        MouseUp += (_, _) => { pressed = false; Invalidate(); };
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float s = DeviceDpi / 96f;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, 10 * s);
        Color fg, border = Color.Empty;
        Brush fill;
        if (!Enabled) { fill = new SolidBrush(Theme.Disabled); fg = Theme.DisabledText; }
        else switch (Kind)
        {
            case Kinds.Primary:
                fill = pressed ? new SolidBrush(Theme.AccentPressed) : hover ? new SolidBrush(Theme.AccentHover)
                     : new LinearGradientBrush(new RectangleF(0, 0, Width, Height + 1), Theme.AccentHover, Theme.Accent, 90f);
                fg = Color.White;
                break;
            case Kinds.Danger:
                fill = new SolidBrush(hover ? Theme.Mix(Theme.BadSoft, Theme.Bad, 0.16) : Theme.BadSoft);
                border = Color.FromArgb(140, Theme.Bad); fg = Theme.Bad;
                break;
            case Kinds.Reward:
                fill = new SolidBrush(hover ? Theme.Mix(Theme.GoldSoft, Theme.Gold, 0.14) : Theme.GoldSoft);
                border = Color.FromArgb(120, Theme.Gold); fg = Theme.Gold;
                break;
            default:
                fill = new SolidBrush(pressed ? Theme.Border : hover ? Theme.Mix(Theme.InputBg, Theme.BorderHi, 0.3) : Theme.InputBg);
                border = hover ? Theme.BorderHi : Theme.Border; fg = Theme.Text;
                break;
        }
        using (fill) g.FillPath(fill, path);
        if (border != Color.Empty) using (var pen = new Pen(border)) g.DrawPath(pen, path);
        if (Enabled && Kind == Kinds.Primary)
            using (var hi = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawLine(hi, 10 * s, 1.5f, Width - 10 * s, 1.5f);
        if (Focused && ShowFocusCues)
            using (var ring = new Pen(Theme.AccentText, 2 * s)) using (var fp = Theme.Round(RectangleF.Inflate(r, -1.5f * s, -1.5f * s), 9 * s)) g.DrawPath(ring, fp);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

// 요약 숫자 칸: 위에서 아래로 아주 옅은 그라데이션 + 위쪽 2px 띠 (돈·보상 칸은 금색, 나머지는 회색)
class KpiTile : Card
{
    readonly Label value = new() { Dock = DockStyle.Top, Height = 38, Font = Theme.Kpi, ForeColor = Theme.Text, Text = "—", BackColor = Color.Transparent, AutoEllipsis = true };
    readonly Label caption, note;
    public Color Strip = Theme.BorderHi;

    public KpiTile(string caption)
    {
        Padding = new Padding(18, 14, 18, 14);
        Controls.Add(note = new Label { Dock = DockStyle.Top, Height = 20, Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Color.Transparent, AutoEllipsis = true });
        Controls.Add(value);
        Controls.Add(this.caption = new Label { Text = caption, Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Color.Transparent, AutoEllipsis = true });
    }

    public void Set(string text, Color? color = null, string note = null, string caption = null)
    {
        value.Text = text;
        value.ForeColor = color ?? Theme.Text;
        if (note != null) this.note.Text = note;
        if (caption != null) this.caption.Text = caption;
        FitValue();
    }

    void FitValue()
    {
        if (value.Width <= 0) return;
        var f = TextRenderer.MeasureText(value.Text, Theme.Kpi).Width <= value.Width ? Theme.Kpi
              : TextRenderer.MeasureText(value.Text, Theme.KpiSmall).Width <= value.Width ? Theme.KpiSmall : Theme.H2;
        if (value.Font != f) value.Font = f;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); FitValue(); }

    protected override void PaintBody(Graphics g, GraphicsPath path, float s)
    {
        using (var grad = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Theme.CardTop, Theme.CardBg, 90f)) g.FillPath(grad, path);
        var clip = g.Clip;
        g.SetClip(path);
        using (var b = new SolidBrush(Strip)) g.FillRectangle(b, 14 * s, 0, Width - 28 * s, 2 * s);
        g.Clip = clip;
    }
}

// 알약 칩·탭. 중립(글자색 Tint = Text): 선택하면 남보라. 색 칩: 옅은 색 바탕 + 섞은 글자색 (TextRenderer는 투명도를 무시하므로 불투명하게 섞는다).
class Pill : Control
{
    public Color Tint = Theme.Muted;
    public bool Selected;
    bool hover;

    public Pill(string text, Color? tint = null, bool clickable = false)
    {
        Text = text;
        Font = Theme.SmallBold;
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
        return new Size(t.Width + (int)(14 * s), t.Height + (int)(6 * s));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Size = GetPreferredSize(Size.Empty); Invalidate(); }

    // 글자로 잰 크기는 이미 화면 배율이 들어가 있으므로, 창 배율 조정 때 다시 키우지 않고 글자로 다시 잰다
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        Size = GetPreferredSize(Size.Empty);
    }

    // 화면 읽기 프로그램·UI 자동화에서 버튼으로 보이고 누를 수 있게
    protected override AccessibleObject CreateAccessibilityInstance() => new PillAccessible(this);

    class PillAccessible : ControlAccessibleObject
    {
        readonly Pill pill;
        public PillAccessible(Pill pill) : base(pill) => this.pill = pill;
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string Name => pill.Text;
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => pill.InvokeOnClick(pill, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, Height / 2f);
        Color fill, border, fg;
        if (Tint == Theme.Text) // 중립: 필터·선택지
        {
            fill = Selected ? Theme.AccentSoft : hover ? Theme.InputBg : Color.Empty;
            border = Selected ? Color.FromArgb(150, Theme.AccentText) : hover ? Theme.BorderHi : Theme.Border;
            fg = Selected || hover ? Theme.Text : Theme.TextSoft;
        }
        else
        {
            fill = Color.FromArgb(Selected ? 56 : hover ? 44 : 30, Tint);
            border = Color.FromArgb(Selected ? 200 : 100, Tint);
            fg = Selected ? Theme.Text : Theme.Mix(Tint, Color.White, 0.24);
        }
        if (fill != Color.Empty) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        using (var pen = new Pen(border)) g.DrawPath(pen, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
        Height = 44;
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
            using var path = Theme.Round(new RectangleF(10 * s, 3 * s, Width - 20 * s, Height - 6 * s), 10 * s);
            using var b = new SolidBrush(active ? Theme.AccentSoft : Theme.SidebarHover);
            g.FillPath(b, path);
        }
        if (active)
            using (var bar = Theme.Round(new RectangleF(4 * s, Height / 2f - 10 * s, 3 * s, 20 * s), 1.5f * s))
            using (var b = new SolidBrush(Theme.AccentText)) g.FillPath(b, bar);
        var icon = active ? Theme.AccentText : hover ? Theme.Text : Theme.SidebarText;
        var fg = active || hover ? Theme.Text : Theme.SidebarText;
        TextRenderer.DrawText(g, Glyph, Theme.Icon, new Rectangle((int)(24 * s), 0, (int)(28 * s), Height), icon,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, Text, active ? Theme.NavBold : Theme.Nav, new Rectangle((int)(60 * s), 0, Width, Height), fg,
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
        Theme.GuideLines(g, left, right, top, bottom, s);
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
        using (var pen = new Pen(color, 2f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
        Theme.EndDot(g, pts[^1], color, s);

        // 기간 등락률(왼쪽 위), 최고·최저(오른쪽 위), 시작·끝 날짜(아래)
        TextRenderer.DrawText(g, $"{(chg >= 0 ? "▲" : "▼")} {chg:+0.00;-0.00;0.00}%", Theme.Bold, new Point((int)left, 0), color);
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
            Theme.Tooltip(g, new RectangleF(tx - 6, ty - 3, size.Width + 12, size.Height + 6), s);
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

        Theme.GuideLines(g, left, right, top, bottom, s);
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
        using (var pen = new Pen(color, 2f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, val);
        Theme.EndDot(g, val[^1], color, s);
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
            Theme.Tooltip(g, new RectangleF(tx - 6, top - 3, size.Width + 12, size.Height + 6), s);
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
        using var axis = new Pen(Theme.GridLine);
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
        using var band = new SolidBrush(Theme.BorderHi);
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
    public override Color SeparatorDark => Theme.GridLine;
    public override Color ImageMarginGradientBegin => Theme.CardBg;
    public override Color ImageMarginGradientMiddle => Theme.CardBg;
    public override Color ImageMarginGradientEnd => Theme.CardBg;
}

// 앱 문양: 남색 둥근 사각형 + 금색 돛(오르는 선이기도 한 항해) + 남보라 물결. 사이드바 로고에 쓴다 (앱 아이콘과 같은 그림).
static class Emblem
{
    public static void Draw(Graphics g, RectangleF r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = r.Width;
        using (var p = Theme.Round(r, w * 0.24f))
        {
            using (var bg = new LinearGradientBrush(r, Color.FromArgb(0x1B, 0x24, 0x40), Color.FromArgb(0x12, 0x1A, 0x2E), 90f)) g.FillPath(bg, p);
            using (var pen = new Pen(Color.FromArgb(120, Theme.Gold))) g.DrawPath(pen, p);
        }
        // 돛: 왼쪽 아래에서 오른쪽 위로 솟는 직각삼각형
        var sail = new[] { new PointF(r.X + w * 0.24f, r.Y + w * 0.70f), new PointF(r.X + w * 0.62f, r.Y + w * 0.70f), new PointF(r.X + w * 0.62f, r.Y + w * 0.18f) };
        using (var gold = new LinearGradientBrush(new RectangleF(r.X, r.Y + w * 0.18f, w, w * 0.52f), Theme.GoldLight, Theme.GoldDeep, 90f)) g.FillPolygon(gold, sail);
        // 물결
        using var wave = new Pen(Theme.AccentHover, Math.Max(1.5f, w * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawBezier(wave, r.X + w * 0.18f, r.Y + w * 0.80f, r.X + w * 0.38f, r.Y + w * 0.72f, r.X + w * 0.58f, r.Y + w * 0.88f, r.X + w * 0.82f, r.Y + w * 0.78f);
    }
}
