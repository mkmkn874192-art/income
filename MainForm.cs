using System.Data;
using System.Text;

namespace KrxScreen;

public class MainForm : Form
{
    static readonly string KeyFile = Path.Combine(Market.Dir, "keys.txt");
    const int PIncome = 0, PMarket = 1, PScreen = 2, PPortfolio = 3, PSettings = 4;

    // 제품명은 여기서만 바꾸면 된다
    public const string AppName = "인컴곳간";
    const string AppEn = "INCOME GOTGAN";
    const string Tagline = "배당·이자·적금, 들어오는 돈이 쌓이는 곳간";

    // keys.txt: 0 공공데이터, 1 DART, 2·3 키움 모의 앱키·시크릿, 4·5 키움 실전 앱키·시크릿
    readonly string[] keys = new string[6];

    // ----- 설정 -----
    readonly TextBox txtGo = new() { Width = 520 };
    readonly TextBox txtDart = new() { Width = 520 };
    readonly TextBox txtAppKey = new() { Width = 520 };
    readonly TextBox txtSecret = new() { Width = 520, UseSystemPasswordChar = true };
    readonly RadioButton rdoMock = new() { Text = "모의투자", AutoSize = true, Checked = true };
    readonly RadioButton rdoReal = new() { Text = "실전 (실제 계좌로 주문)", AutoSize = true };
    readonly Button btnSave = Theme.Primary("저장");
    readonly Label lblSettings = Theme.Text_("", Theme.Body, Theme.Good);
    bool reverting;

    // ----- 시장 -----
    readonly TextBox txtSearch = new()
    {
        BorderStyle = BorderStyle.None, Font = Theme.Search, BackColor = Theme.InputBg, ForeColor = Theme.Text,
        PlaceholderText = "종목명 · 코드 · 라벨 검색   예) 배당주, 가치주 코스피, 삼성, 005930",
    };
    readonly FlowLayoutPanel chipBar = new() { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Bg, Padding = new Padding(0, 10, 0, 0) };
    readonly List<Pill> marketPills = new();
    readonly Label lblMarket = Theme.Text_("시세를 불러오는 중...", Theme.Small, Theme.Muted);
    readonly DataGridView gridMarket = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };
    readonly System.Windows.Forms.Timer searchDelay = new() { Interval = 200 };
    string marketFilter; // null 전체, KOSPI, KOSDAQ
    string basDt;
    List<Quote> quotes;
    List<StockRow> allRows = new();
    Dictionary<string, StockRow> rowByCode = new();
    DataTable screenTable;
    Dictionary<string, StockSeries> series = new();

    // 상세 패널
    readonly Label dName = new() { Dock = DockStyle.Top, Height = 34, Font = Theme.H1, ForeColor = Theme.Text, Text = "종목을 선택하세요", AutoEllipsis = true };
    readonly Label dSub = new() { Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted };
    readonly Label dPrice = new() { Dock = DockStyle.Top, Height = 38, Font = Theme.Kpi, ForeColor = Theme.Text };
    readonly FlowLayoutPanel dLabels = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 6, 0, 4) };
    readonly TableLayoutPanel dMetrics = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 4, 0, 8) };
    readonly Dictionary<string, Label> metric = new();
    readonly Label dDivCalc = new() { Dock = DockStyle.Top, Height = 86, Font = Theme.Body, ForeColor = Theme.Text };
    readonly NumericUpDown numAddQty = new() { Minimum = 1, Maximum = 10_000_000, Value = 10, Width = 90, ThousandsSeparator = true };
    readonly Button btnAdd = Theme.Primary("배당 수입에 담기");
    readonly BarSeries niChart = new(Theme.Accent, "억", 1e8);
    readonly BarSeries dpsChart = new(Color.FromArgb(52, 211, 153), "원", 1);

    // ----- 스크리닝 -----
    readonly NumericUpDown numPrice = new() { Minimum = 0, Maximum = 10_000_000, Increment = 1000, Value = 5000, Width = 100, ThousandsSeparator = true };
    readonly NumericUpDown numCap = new() { Minimum = 0, Maximum = 1000, DecimalPlaces = 1, Increment = 0.5m, Value = 1, Width = 70 };
    readonly Button btnRun = Theme.Primary("분석 실행");
    readonly Button btnCsv = Theme.Secondary("CSV 내보내기");
    readonly ProgressBar progress = new() { Style = ProgressBarStyle.Marquee, Width = 140, Height = 8, Visible = false, Margin = new Padding(4, 14, 8, 0) };
    readonly Label lblStatus = Theme.Text_("[분석 실행]을 누르면 3~5분 정도 걸려요.", Theme.Body, Theme.Muted);
    readonly DataGridView grid = new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
    };
    DataTable result;
    Font boldFont;

    // ----- 포트폴리오 -----
    readonly Button btnBalance = Theme.Primary("잔고 조회 · 배분 계산");
    readonly Button btnOrder = Theme.Danger("매수 주문 실행");
    readonly Label lblPf = Theme.Text_("[잔고 조회 · 배분 계산]을 누르세요.", Theme.Body, Theme.Muted);
    readonly KpiTile kpiTotal = new("총자산");
    readonly KpiTile kpiCash = new("예수금");
    readonly KpiTile kpiOrderable = new("주문가능금액 (이번 달 적립액)");
    readonly KpiTile kpiMode = new("이번 달 배분 방식");
    readonly WeightChart chart = new();
    readonly DataGridView gridSum = NewGrid(true);
    readonly DataGridView gridHold = NewGrid(true);
    readonly DataGridView gridPlan = NewGrid(false);
    readonly TextBox txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Theme.CardBg, ForeColor = Theme.Text, Font = Theme.Small };
    DataTable plan;
    List<Kiwoom.Holding> holdings = new();
    double totalAsset, orderable;
    Kiwoom kw;
    string kwId;

    // ----- 레이아웃 -----
    readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    readonly List<(NavItem nav, Control page)> pages = new();
    readonly Label modeBadge = new() { Dock = DockStyle.Bottom, Height = 48, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Bold };

    public MainForm()
    {
        // 아래 픽셀 값은 100% 배율 기준. 고배율 화면에서는 ResumeLayout 때 DPI 비율만큼 키운다.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"{AppName} — {Tagline}";
        Width = 1400;
        Height = 880;
        MinimumSize = new Size(1150, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Portfolio.LoadTargets();
        Font = Theme.Body;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Theme.DarkTitleBar(this);

        foreach (var g in new[] { grid, gridMarket, gridSum, gridHold, gridPlan }) Theme.StyleGrid(g);
        foreach (Control c in new Control[] { txtGo, txtDart, txtAppKey, txtSecret, numPrice, numCap, rdoMock, rdoReal, numAddQty }) Theme.StyleInput(c);
        rdoReal.ForeColor = Theme.Bad;
        Theme.DarkScrollbars(txtLog);
        grid.CellFormatting += Grid_CellFormatting;
        gridMarket.CellFormatting += GridMarket_CellFormatting;
        gridMarket.SelectionChanged += (_, _) => ShowDetail();
        foreach (var g in new[] { gridSum, gridHold, gridPlan }) g.DataBindingComplete += (s, _) => FormatNumbers((DataGridView)s);
        btnCsv.Enabled = btnOrder.Enabled = false;

        Controls.Add(content);
        Controls.Add(BuildSidebar());
        AddPage("", "배당 수입", BuildIncomePage());
        AddPage("", "시장", BuildMarketPage());
        AddPage("", "가치 분석", BuildScreenPage());
        AddPage("", "포트폴리오", BuildPortfolioPage());
        AddPage("", "설정", BuildSettingsPage());

        btnSave.Click += (_, _) => { SaveKwKeys(); lblSettings.Text = "저장했어요. 다음 실행부터 자동으로 불러와요."; };
        btnRun.Click += async (_, _) => await RunScreen();
        btnCsv.Click += (_, _) => ExportCsv();
        btnBalance.Click += async (_, _) => await LoadPortfolio();
        btnOrder.Click += async (_, _) => await PlaceOrders();
        rdoReal.CheckedChanged += (_, _) => OnModeChanged();
        txtSearch.TextChanged += (_, _) => { searchDelay.Stop(); searchDelay.Start(); };
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); ApplySearch(); };

        LoadKeys();
        UpdateModeBadge();
        ShowPage(string.IsNullOrEmpty(keys[0]) || string.IsNullOrEmpty(keys[1]) ? PSettings : PIncome);
        ResumeLayout(false);

        // 열 폭 조정은 표가 화면에 만들어진 뒤에만 된다
        Shown += async (_, _) =>
        {
            RefreshIncome();
            LoadSavedData();
            await RefreshQuotes();
        };
    }

    // ================= 레이아웃 =================

    readonly Panel navPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };

    Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Theme.Sidebar };
        var logo = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(24, 22, 0, 0) };
        logo.Controls.Add(new Label { Text = AppEn, Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted });
        logo.Controls.Add(new Label { Text = AppName, Dock = DockStyle.Top, Height = 34, Font = Theme.Logo, ForeColor = Color.White });
        side.Controls.Add(navPanel);
        side.Controls.Add(modeBadge);
        side.Controls.Add(logo);
        return side;
    }

    void AddPage(string glyph, string title, Control page)
    {
        int index = pages.Count;
        var nav = new NavItem(glyph, title);
        nav.Click += (_, _) => ShowPage(index);
        navPanel.Controls.Add(nav);
        nav.BringToFront(); // Dock=Top은 나중에 앞으로 온 것이 아래로 쌓인다
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        content.Controls.Add(page);
        pages.Add((nav, page));
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].page.Visible = i == index;
            pages[i].nav.Active = i == index;
        }
    }

    static Panel Page() => new() { BackColor = Theme.Bg, Padding = new Padding(28, 24, 16, 16) };

    static Control Header(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 66 };
        p.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Top, Height = 22, Font = Theme.Body, ForeColor = Theme.Muted });
        p.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 38, Font = Theme.H1, ForeColor = Theme.Text });
        return p;
    }

    static FlowLayoutPanel Toolbar(params Control[] items)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Bg, Margin = Padding.Empty };
        f.Controls.AddRange(items);
        return f;
    }

    Control BuildMarketPage()
    {
        var page = Page();

        // 검색창: 아이콘 + 입력칸을 둥근 어두운 상자에
        var searchBox = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.InputBg, Padding = new Padding(14, 12, 14, 8) };
        searchBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(txtSearch.Focused ? Theme.Accent : Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, searchBox.Width - 1, searchBox.Height - 1);
        };
        txtSearch.GotFocus += (_, _) => searchBox.Invalidate();
        txtSearch.LostFocus += (_, _) => searchBox.Invalidate();
        txtSearch.Dock = DockStyle.Fill;
        searchBox.Controls.Add(txtSearch);
        searchBox.Controls.Add(new Label { Text = "", Font = Theme.Icon, ForeColor = Theme.Muted, Dock = DockStyle.Left, Width = 30 });

        foreach (var (label, code) in new[] { ("전체", (string)null), ("코스피", "KOSPI"), ("코스닥", "KOSDAQ") })
        {
            var p = new Pill(label, Theme.Text, clickable: true) { Selected = code == null, Tag = code };
            p.Click += (_, _) =>
            {
                marketFilter = code;
                foreach (var m in marketPills) { m.Selected = m == p; m.Invalidate(); }
                ApplySearch();
            };
            marketPills.Add(p);
            chipBar.Controls.Add(p);
        }
        chipBar.Controls.Add(new Label { Width = 14, Height = 10 });
        foreach (var l in Labels.Chips)
        {
            var p = new Pill(l, Theme.LabelColor(l), clickable: true);
            p.Click += (_, _) => { txtSearch.Text = l; txtSearch.SelectionStart = l.Length; };
            chipBar.Controls.Add(p);
        }

        var detail = BuildDetailPanel();
        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(new Card(null, gridMarket) { Padding = new Padding(1), Margin = Padding.Empty });
        body.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 12 });
        body.Controls.Add(detail);

        var status = new Panel { Dock = DockStyle.Top, Height = 30 };
        lblMarket.Dock = DockStyle.Fill;
        status.Controls.Add(lblMarket);

        page.Controls.Add(body);
        page.Controls.Add(status);
        page.Controls.Add(chipBar);
        page.Controls.Add(Theme.Spacer(4));
        page.Controls.Add(searchBox);
        page.Controls.Add(Header("시장", "코스피·코스닥 전 종목을 한눈에. 이름·코드는 물론 '배당주', '가치주' 같은 말로도 찾을 수 있어요."));
        return page;
    }

    Control BuildDetailPanel()
    {
        string[] names = { "시총", "거래대금", "PER", "PBR", "ROE", "배당수익률", "부채비율", "이익성장", "점수", "배당 연수" };
        for (int i = 0; i < 4; i++) dMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        foreach (var n in names)
        {
            dMetrics.Controls.Add(new Label { Text = n, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 6, 4, 0) });
            var v = new Label { Text = "—", AutoSize = true, Font = Theme.Bold, ForeColor = Theme.Text, Margin = new Padding(0, 5, 8, 0) };
            dMetrics.Controls.Add(v);
            metric[n] = v;
        }

        var inner = new Panel { Dock = DockStyle.Fill };
        Label Section(string t) => new() { Text = t, Dock = DockStyle.Top, Height = 28, Font = Theme.H2, ForeColor = Theme.Text, Padding = new Padding(0, 8, 0, 0) };
        // Dock=Top은 나중에 추가한 것이 위로 간다 → 아래쪽부터 추가
        var addRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        addRow.Controls.AddRange(new Control[] { numAddQty, Theme.Text_("주"), btnAdd });
        btnAdd.Click += async (_, _) => await AddToIncome();

        inner.Controls.Add(dpsChart);
        inner.Controls.Add(Section("주당배당금 추이 (원)"));
        inner.Controls.Add(niChart);
        inner.Controls.Add(Section("순이익 추이 (억원)"));
        inner.Controls.Add(addRow);
        inner.Controls.Add(dDivCalc);
        inner.Controls.Add(Section("배당으로 받을 돈"));
        inner.Controls.Add(dMetrics);
        inner.Controls.Add(dPrice);
        inner.Controls.Add(dSub);
        inner.Controls.Add(dName);
        return new Card(null, inner) { Dock = DockStyle.Right, Width = 380, Margin = Padding.Empty, Padding = new Padding(18, 14, 18, 14) };
    }

    Control BuildScreenPage()
    {
        var page = Page();
        page.Controls.Add(new Card(null, grid) { Margin = Padding.Empty, Padding = new Padding(1) });
        page.Controls.Add(Theme.Spacer(14));
        page.Controls.Add(Toolbar(Theme.Text_("주가 하한(원)"), numPrice, Theme.Text_("   시총 하한(조)"), numCap, Theme.Text_("   "),
            btnRun, btnCsv, progress, lblStatus));
        page.Controls.Add(Header("가치 분석", "10년 재무와 배당 이력을 그레이엄의 6가지 기준으로 채점해요. 결과는 저장되어 시장 검색과 배당 계산에 쓰여요."));
        return page;
    }

    Control BuildPortfolioPage()
    {
        var page = Page();

        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 4, Margin = Padding.Empty };
        for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpis.Controls.AddRange(new Control[] { kpiTotal, kpiCash, kpiOrderable, kpiMode });

        var weight = new Panel { Dock = DockStyle.Fill };
        weight.Controls.Add(gridSum);
        gridSum.Dock = DockStyle.Fill;
        weight.Controls.Add(Theme.Spacer(6));
        weight.Controls.Add(chart);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        body.Controls.Add(new Card("목표 대비 비중  ·  회색 띠 = 허용 범위", weight), 0, 0);
        body.Controls.Add(new Card("이번 달 매수안  ·  셀을 고쳐서 조정", gridPlan), 1, 0);
        body.Controls.Add(new Card("보유 종목", gridHold), 0, 1);
        body.Controls.Add(new Card("주문 기록", txtLog), 1, 1);

        page.Controls.Add(body);
        page.Controls.Add(kpis);
        page.Controls.Add(Theme.Spacer(12));
        page.Controls.Add(Toolbar(btnBalance, btnOrder, lblPf));
        page.Controls.Add(Header("포트폴리오", "목표 비중에 맞춰 이번 달 적립금을 나눠요. 목표는 [설정]에서 바꿀 수 있고, 주문은 확인창을 거친 지정가 매수만 나가요."));
        return page;
    }

    Control BuildSettingsPage()
    {
        var page = Page();
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        void Row(string label, Control c, string hint = null)
        {
            form.Controls.Add(Theme.Text_(label, Theme.Bold));
            var cell = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 4, 0, 10) };
            cell.Controls.Add(c);
            if (hint != null) cell.Controls.Add(Theme.Text_(hint, Theme.Small, Theme.Muted));
            form.Controls.Add(cell);
        }
        var modes = new FlowLayoutPanel { AutoSize = true };
        modes.Controls.AddRange(new Control[] { rdoMock, rdoReal });

        Row("공공데이터포털 인증키", txtGo, "data.go.kr → 금융위원회_주식시세정보 활용신청 후 '일반 인증키'");
        Row("OpenDART 키", txtDart, "opendart.fss.or.kr → 인증키 신청");
        Row("키움 계좌 모드", modes, "모의투자와 실전은 앱키가 달라요. 모드를 바꾸면 그 모드의 키를 불러와요.");
        Row("키움 앱키", txtAppKey, "openapi.kiwoom.com → APP Key 관리");
        Row("키움 시크릿키", txtSecret);
        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.AddRange(new Control[] { btnSave, lblSettings });
        form.Controls.Add(new Label());
        form.Controls.Add(actions);

        var card = new Card("API 키", form) { Dock = DockStyle.Top, Height = 480, Margin = Padding.Empty };

        // 포트폴리오 목표
        var tgt = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        for (int i = 0; i < Portfolio.Cats.Length; i++)
        {
            numTarget[i] = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Increment = 5, Width = 70, Value = (decimal)(Portfolio.Target[i] * 100) };
            Theme.StyleInput(numTarget[i]);
            tgt.Controls.Add(Theme.Text_((i == 0 ? "" : "   ") + Portfolio.Cats[i] + " %", Theme.Bold));
            tgt.Controls.Add(numTarget[i]);
        }
        numBand.Value = (decimal)(Portfolio.Band * 100);
        numSatMax.Value = (decimal)(Portfolio.SatelliteMax * 100);
        Theme.StyleInput(numBand);
        Theme.StyleInput(numSatMax);
        var tgt2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        var btnTargets = Theme.Primary("목표 저장");
        var lblTargets = Theme.Text_("", Theme.Body, Theme.Good);
        tgt2.Controls.AddRange(new Control[] { Theme.Text_("리밸런싱 허용 폭 ±", Theme.Bold), numBand, Theme.Text_("%p     위성 한 종목 한도", Theme.Bold), numSatMax,
            Theme.Text_("% (총자산 대비)     ", Theme.Bold), btnTargets, lblTargets });
        btnTargets.Click += (_, _) =>
        {
            double sum = numTarget.Sum(n => (double)n.Value);
            if (Math.Abs(sum - 100) > 0.05)
            {
                lblTargets.ForeColor = Theme.Bad;
                lblTargets.Text = $"네 분류 합이 100%여야 해요 (지금 {sum:0.#}%).";
                return;
            }
            Portfolio.Target = numTarget.Select(n => (double)n.Value / 100).ToArray();
            Portfolio.Band = (double)numBand.Value / 100;
            Portfolio.SatelliteMax = (double)numSatMax.Value / 100;
            Portfolio.SaveTargets();
            chart.Invalidate();
            lblTargets.ForeColor = Theme.Good;
            lblTargets.Text = "저장했어요. 다음 [잔고 조회 · 배분 계산]부터 적용돼요.";
        };
        var tgtBody = new Panel { Dock = DockStyle.Fill };
        tgtBody.Controls.Add(tgt2);
        tgtBody.Controls.Add(tgt);
        var tgtCard = new Card("포트폴리오 목표 비중", tgtBody) { Dock = DockStyle.Top, Height = 140, Margin = Padding.Empty };

        var note = Theme.Text_($"키와 설정은 이 PC의 {Market.Dir} 폴더에만 저장돼요.\r\n" +
            $"보유 종목 분류를 바꾸려면 {Portfolio.ClassFile} 에 '종목코드,분류[,매수]'를 적어요 (분류: 채권/지수/배당/위성).",
            Theme.Small, Theme.Muted);
        note.Dock = DockStyle.Top;
        page.Controls.Add(note);
        page.Controls.Add(Theme.Spacer(8));
        page.Controls.Add(tgtCard);
        page.Controls.Add(Theme.Spacer(12));
        page.Controls.Add(card);
        page.Controls.Add(Header("설정", "API 키, 계좌 모드, 포트폴리오 목표"));
        return page;
    }

    readonly NumericUpDown[] numTarget = new NumericUpDown[4];
    readonly NumericUpDown numBand = new() { Minimum = 1, Maximum = 30, DecimalPlaces = 1, Width = 60 };
    readonly NumericUpDown numSatMax = new() { Minimum = 0.5m, Maximum = 100, DecimalPlaces = 1, Width = 60 };

    static DataGridView NewGrid(bool readOnly) => new()
    {
        ReadOnly = readOnly,
        AllowUserToAddRows = !readOnly,
        AllowUserToDeleteRows = !readOnly,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };

    static void FormatNumbers(DataGridView g)
    {
        foreach (DataGridViewColumn c in g.Columns)
            if (c.ValueType == typeof(double))
            {
                c.DefaultCellStyle.Format = c.Name.Contains('%') ? "0.0" : "N0";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
    }

    // ================= 키 =================

    void LoadKeys()
    {
        if (File.Exists(KeyFile))
        {
            var lines = File.ReadAllLines(KeyFile);
            for (int i = 0; i < keys.Length && i < lines.Length; i++) keys[i] = lines[i];
        }
        txtGo.Text = keys[0];
        txtDart.Text = keys[1];
        ShowKwKeys();
    }

    void SaveKeys()
    {
        keys[0] = txtGo.Text.Trim();
        keys[1] = txtDart.Text.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(KeyFile));
        File.WriteAllLines(KeyFile, keys.Select(k => k ?? ""));
    }

    int KwSlot => rdoReal.Checked ? 4 : 2;
    string Mode => rdoReal.Checked ? "실전" : "모의투자";

    void ShowKwKeys()
    {
        txtAppKey.Text = keys[KwSlot];
        txtSecret.Text = keys[KwSlot + 1];
    }

    void SaveKwKeys()
    {
        keys[KwSlot] = txtAppKey.Text.Trim();
        keys[KwSlot + 1] = txtSecret.Text.Trim();
        SaveKeys();
    }

    void OnModeChanged()
    {
        if (reverting) return;
        if (rdoReal.Checked && MessageBox.Show("실전 모드에서는 실제 계좌로 주문이 나가요. 바꿀까요?", "실전 전환",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            reverting = true;
            rdoMock.Checked = true;
            reverting = false;
            return;
        }
        int prev = rdoReal.Checked ? 2 : 4; // 바꾸기 전 모드의 입력값은 메모리에 보관
        keys[prev] = txtAppKey.Text.Trim();
        keys[prev + 1] = txtSecret.Text.Trim();
        ShowKwKeys();
        plan = null;
        gridPlan.DataSource = null;
        btnOrder.Enabled = false;
        UpdateModeBadge();
    }

    void UpdateModeBadge()
    {
        modeBadge.Text = rdoReal.Checked ? "●  실전 계좌" : "●  모의투자";
        modeBadge.ForeColor = rdoReal.Checked ? Theme.Bad : Theme.Good;
        modeBadge.BackColor = rdoReal.Checked ? Color.FromArgb(40, 12, 12) : Color.FromArgb(10, 32, 18);
    }

    // ================= 배당 수입 =================

    readonly KpiTile kYear = new("1년"), kMonth = new("한 달"), kWeek = new("한 주"), kDay = new("하루");
    readonly DataGridView gridIncome = NewGrid(false);
    readonly NumericUpDown numGoal = new() { Minimum = 1, Maximum = 100_000, Value = 100, Width = 90, ThousandsSeparator = true };
    readonly Label lblGoal = Theme.Text_("", Theme.Body, Theme.Text);
    readonly Panel goalBar = new() { Dock = DockStyle.Top, Height = 10 };
    readonly Label lblIncome = Theme.Text_("", Theme.Small, Theme.Muted);
    readonly Pill pillPre = new("세전", Theme.Text, clickable: true) { Selected = true };
    readonly Pill pillPost = new("세후 (15.4%)", Theme.Text, clickable: true);
    List<IncomeItem> incomeItems = Income.Load();
    DataTable incomeTable;
    double goalProgress;
    bool afterTax;

    Control BuildIncomePage()
    {
        var page = Page();
        Theme.StyleGrid(gridIncome);
        gridIncome.AllowUserToAddRows = false;
        gridIncome.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            string n = gridIncome.Columns[e.ColumnIndex].Name;
            if (ColFormat.TryGetValue(n, out var f)) e.CellStyle.Format = f;
            if (n is "수량" or "현재가(원)" or "주당배당금") e.CellStyle.BackColor = Theme.InputBg; // 고칠 수 있는 칸
            if (n is "연 배당" or "월 배당") { e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153); e.CellStyle.Font = Theme.Bold; }
            if (n == "출처") e.CellStyle.ForeColor = Theme.Muted;
        };
        foreach (Control c in new Control[] { numGoal }) Theme.StyleInput(c);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, ColumnCount = 4, Margin = Padding.Empty };
        for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpis.Controls.AddRange(new Control[] { kYear, kMonth, kWeek, kDay });

        var btnImport = Theme.Secondary("키움 보유종목 불러오기");
        var btnRemove = Theme.Secondary("선택 삭제");
        btnImport.Click += async (_, _) => await ImportHoldings();
        btnRemove.Click += (_, _) =>
        {
            var codes = gridIncome.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Cells["코드"].Value as string).ToHashSet();
            if (codes.Count == 0) return;
            incomeItems.RemoveAll(i => codes.Contains(i.Code));
            Income.Save(incomeItems);
            RefreshIncome();
        };
        pillPre.Click += (_, _) => { afterTax = false; pillPre.Selected = true; pillPost.Selected = false; pillPre.Invalidate(); pillPost.Invalidate(); RefreshIncome(); };
        pillPost.Click += (_, _) => { afterTax = true; pillPre.Selected = false; pillPost.Selected = true; pillPre.Invalidate(); pillPost.Invalidate(); RefreshIncome(); };
        pillPre.Margin = pillPost.Margin = new Padding(0, 6, 6, 0);

        // 목표 월 배당 계산
        goalBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(Theme.CardBg);
            using var track = new SolidBrush(Theme.InputBg);
            using var fill = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.FillRectangle(track, 0, 2, goalBar.Width, goalBar.Height - 4);
            g.FillRectangle(fill, 0, 2, (float)(goalBar.Width * Math.Clamp(goalProgress, 0, 1)), goalBar.Height - 4);
        };
        numGoal.ValueChanged += (_, _) => RefreshIncome();
        var goalRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        goalRow.Controls.AddRange(new Control[] { Theme.Text_("한 달에"), numGoal, Theme.Text_("만원을 받으려면"), lblGoal });
        var goalBody = new Panel { Dock = DockStyle.Fill };
        goalBody.Controls.Add(goalBar);
        goalBody.Controls.Add(Theme.Spacer(6));
        goalBody.Controls.Add(goalRow);
        var goalCard = new Card("목표 배당", goalBody) { Dock = DockStyle.Top, Height = 110, Margin = Padding.Empty };

        var listCard = new Card("내 배당 목록  ·  수량·현재가·주당배당금 칸은 직접 고칠 수 있어요", gridIncome) { Margin = Padding.Empty };

        page.Controls.Add(listCard);
        page.Controls.Add(Theme.Spacer(12));
        page.Controls.Add(goalCard);
        page.Controls.Add(Theme.Spacer(12));
        page.Controls.Add(kpis);
        var status = new Panel { Dock = DockStyle.Top, Height = 26 };
        lblIncome.Dock = DockStyle.Fill;
        status.Controls.Add(lblIncome);
        page.Controls.Add(status);
        page.Controls.Add(Toolbar(btnImport, btnRemove, Theme.Text_("     "), pillPre, pillPost));
        page.Controls.Add(Header("배당 수입", "담아 둔 종목이 지난 결산만큼 배당을 준다고 보고, 하루·한 주·한 달·1년에 들어올 돈을 계산해요."));
        return page;
    }

    static string Krw(double v) =>
        v >= 1e8 ? $"{Math.Floor(v / 1e8):N0}억 {Math.Floor(v % 1e8 / 1e4):N0}만원"
        : v >= 1e4 ? $"{Math.Floor(v / 1e4):N0}만 {v % 1e4:N0}원" : $"{v:N0}원";

    void RefreshIncome()
    {
        incomeTable = new DataTable();
        incomeTable.Columns.Add("종목명"); incomeTable.Columns.Add("코드");
        incomeTable.Columns.Add("수량", typeof(double));
        incomeTable.Columns.Add("현재가(원)", typeof(double));
        incomeTable.Columns.Add("평가금액", typeof(double), "[수량] * [현재가(원)]");
        incomeTable.Columns.Add("주당배당금", typeof(double));
        incomeTable.Columns.Add("배당률%", typeof(double), "IIF([현재가(원)] > 0, [주당배당금] / [현재가(원)] * 100, 0)");
        incomeTable.Columns.Add("연 배당", typeof(double), "[수량] * [주당배당금]");
        incomeTable.Columns.Add("월 배당", typeof(double), "[수량] * [주당배당금] / 12");
        incomeTable.Columns.Add("출처");
        foreach (var i in incomeItems)
        {
            var row = incomeTable.NewRow(); // 계산 열에는 값을 넣지 않는다
            row["종목명"] = i.Name; row["코드"] = i.Code; row["수량"] = i.Qty; row["현재가(원)"] = i.Price;
            row["주당배당금"] = i.Dps; row["출처"] = i.Source;
            incomeTable.Rows.Add(row);
        }
        incomeTable.ColumnChanged += (_, e) =>
        {
            var item = incomeItems.FirstOrDefault(i => i.Code == (e.Row["코드"] as string));
            if (item == null) return;
            double v = e.ProposedValue is double d ? d : 0;
            if (e.Column.ColumnName == "수량") item.Qty = v;
            else if (e.Column.ColumnName == "현재가(원)") item.Price = v;
            else if (e.Column.ColumnName == "주당배당금") { item.Dps = v; item.Source = "직접 입력"; }
            else return;
            Income.Save(incomeItems);
            BeginInvoke(new Action(RefreshIncome)); // 편집이 끝난 뒤 합계 다시 계산
        };
        gridIncome.DataSource = incomeTable;
        foreach (DataGridViewColumn c in gridIncome.Columns)
            c.ReadOnly = c.Name is not ("수량" or "현재가(원)" or "주당배당금");
        FitColumns(gridIncome, incomeTable, "종목명");

        double annual = incomeItems.Sum(i => i.Qty * i.Dps) * (afterTax ? 1 - Income.TaxRate : 1);
        double invested = incomeItems.Sum(i => i.Qty * i.Price);
        double yield = invested > 0 ? annual / invested : 0;
        string tax = afterTax ? "세후" : "세전";
        kYear.Set(Krw(annual), Color.FromArgb(52, 211, 153), $"{tax} 예상", "1년");
        kMonth.Set(Krw(annual / 12), null, "1년 ÷ 12 (월 평균)");
        kWeek.Set(Krw(annual / 52), null, "1년 ÷ 52");
        kDay.Set(Krw(annual / 365), null, "1년 ÷ 365");

        double goal = (double)numGoal.Value * 1e4 * 12; // 연간 목표
        if (yield > 0)
        {
            double need = goal / yield;
            goalProgress = invested / need;
            lblGoal.Text = $"평균 배당률 {yield * 100:0.00}%({tax}) 기준  {Krw(need)} 이 필요해요.  지금 {Krw(invested)} · {goalProgress * 100:0.0}% 달성";
        }
        else
        {
            goalProgress = 0;
            lblGoal.Text = "배당 목록에 종목을 담으면 필요한 투자금을 계산해요.";
        }
        goalBar.Invalidate();
        lblIncome.Text = incomeItems.Count == 0
            ? "아직 담은 종목이 없어요. [시장]에서 종목을 고른 뒤 [배당 수입에 담기]를 누르거나, 키움 보유종목을 불러오세요."
            : $"{incomeItems.Count}종목 · 투자금 {Krw(invested)} · 평균 배당률 {yield * 100:0.00}% ({tax})  ·  추정치예요. 실제 배당은 회사 결정에 따라 달라지고, 월·주·일 금액은 1년치를 고르게 나눈 평균이에요.";
    }

    // 스크리닝 때 모은 가장 최근 연도의 주당배당금
    double? LatestDps(string code)
    {
        var d = series.GetValueOrDefault(code)?.Dps;
        if (d != null && d.Count > 0) return d[d.Keys.Max()];
        return incomeItems.FirstOrDefault(i => i.Code == code)?.Dps;
    }

    // 마지막 결산 배당이 직전 3년 평균의 2배를 넘으면 일회성 특별배당일 수 있다
    bool MaybeSpecial(string code)
    {
        var d = series.GetValueOrDefault(code)?.Dps;
        if (d == null || d.Count < 2) return false;
        int last = d.Keys.Max();
        var prev = d.Where(kv => kv.Key < last && kv.Key >= last - 3).Select(kv => kv.Value).ToList();
        return prev.Count > 0 && prev.Average() > 0 && d[last] > prev.Average() * 2;
    }

    async Task<(double dps, string source)> FindDps(string code)
    {
        var d = series.GetValueOrDefault(code)?.Dps;
        if (d != null && d.Count > 0)
        {
            int y = d.Keys.Max();
            return (d[y], $"DART {y} 결산" + (MaybeSpecial(code) ? " · ⚠ 특별배당 가능성" : ""));
        }
        if (string.IsNullOrWhiteSpace(keys[1])) return (0, "직접 입력 필요");
        var r = await Income.LatestDps(keys[1], code);
        return r == null ? (0, "직접 입력 필요 (ETF 등)") : (r.Value.dps, $"DART {r.Value.year} 결산");
    }

    async Task AddToIncome()
    {
        if (gridMarket.CurrentRow?.Cells["종목코드"].Value is not string code || !rowByCode.TryGetValue(code, out var r)) return;
        btnAdd.Enabled = false;
        try
        {
            var item = incomeItems.FirstOrDefault(i => i.Code == code);
            if (item == null)
            {
                var (dps, src) = await FindDps(code);
                item = new IncomeItem { Code = code, Name = r.Q.Name, Dps = dps, Source = src };
                incomeItems.Add(item);
            }
            item.Qty += (double)numAddQty.Value;
            item.Price = r.Q.Price;
            Income.Save(incomeItems);
            RefreshIncome();
            lblMarket.Text = $"'{r.Q.Name}' {numAddQty.Value:N0}주를 배당 수입에 담았어요. (지금 {item.Qty:N0}주)";
        }
        catch (Exception ex)
        {
            MessageBox.Show("배당 정보를 못 가져왔어요: " + ex.Message, "오류");
        }
        finally
        {
            btnAdd.Enabled = true;
        }
    }

    async Task ImportHoldings()
    {
        if (txtAppKey.Text.Trim() == "" || txtSecret.Text.Trim() == "")
        {
            MessageBox.Show($"설정에서 키움 {Mode} 앱키와 시크릿키를 넣어주세요.", "키 없음");
            ShowPage(PSettings);
            return;
        }
        try
        {
            lblIncome.Text = $"[{Mode}] 키움 보유종목을 불러오는 중...";
            holdings = await GetClient().GetHoldings();
            foreach (var h in holdings)
            {
                var item = incomeItems.FirstOrDefault(i => i.Code == h.Code);
                if (item == null)
                {
                    var (dps, src) = await FindDps(h.Code);
                    incomeItems.Add(item = new IncomeItem { Code = h.Code, Name = h.Name, Dps = dps, Source = src });
                }
                item.Qty = h.Qty;
                item.Price = h.CurPrice;
            }
            Income.Save(incomeItems);
            RefreshIncome();
        }
        catch (Exception ex)
        {
            lblIncome.Text = "불러오기 실패";
            MessageBox.Show(ex.Message, "오류");
        }
    }

    // ================= 시장 =================

    // 지난번 시세·스크리닝 결과를 바로 띄운다 (네트워크 없이)
    void LoadSavedData()
    {
        try
        {
            if (File.Exists(Market.ScreenFile))
            {
                screenTable = new DataTable();
                screenTable.ReadXml(Market.ScreenFile);
                ShowScreenResult(screenTable, $"지난 결과 ({File.GetLastWriteTime(Market.ScreenFile):yyyy-MM-dd HH:mm})");
            }
        }
        catch { screenTable = null; }
        series = Market.LoadSeries();
        (basDt, quotes) = Market.LoadCached();
        if (quotes != null) RebuildRows();
    }

    async Task RefreshQuotes()
    {
        if (string.IsNullOrWhiteSpace(keys[0])) { lblMarket.Text = "설정에서 공공데이터포털 키를 넣으면 전 종목 시세가 보여요."; return; }
        try
        {
            if (quotes == null) lblMarket.Text = "전 종목 시세를 받는 중...";
            var (d, q) = await Market.Fetch(keys[0], Screener.NewHttp());
            if (d != basDt || quotes == null)
            {
                (basDt, quotes) = (d, q);
                RebuildRows();
            }
        }
        catch (Exception ex)
        {
            lblMarket.Text = quotes == null ? "시세를 못 받았어요: " + ex.Message.Split('\n')[0] : lblMarket.Text + "  (최신 시세 갱신 실패)";
        }
    }

    void RebuildRows()
    {
        allRows = Labels.Build(quotes, screenTable);
        rowByCode = allRows.ToDictionary(r => r.Q.Code);
        ApplySearch();

        // 배당 목록 현재가를 최신 시세로 (시세 API에 없는 ETF는 그대로)
        foreach (var i in incomeItems)
            if (rowByCode.TryGetValue(i.Code, out var r)) i.Price = r.Q.Price;
        RefreshIncome();
    }

    void ApplySearch()
    {
        if (quotes == null) return;
        var source = marketFilter == null ? allRows : allRows.Where(r => r.Q.Market == marketFilter);
        var found = Labels.Search(source, txtSearch.Text, out var rule);

        var t = new DataTable();
        t.Columns.Add("종목코드"); t.Columns.Add("시장"); t.Columns.Add("종목명");
        foreach (var c in new[] { "시가총액(억)", "현재가", "등락%", "배당%" }) t.Columns.Add(c, typeof(double));
        foreach (var r in found)
        {
            double? dps = LatestDps(r.Q.Code);
            double? div = dps > 0 && r.Q.Price > 0 ? dps / r.Q.Price * 100 : r.Div; // 현재가 기준 배당률
            t.Rows.Add(r.Q.Code, r.Q.Market == "KOSPI" ? "코스피" : "코스닥", r.Q.Name, Math.Round(r.Q.Cap / 1e8),
                r.Q.Price, r.Q.ChangeRate, div.HasValue ? div.Value : DBNull.Value);
        }
        gridMarket.DataSource = t;
        FitColumns(gridMarket, t, "종목명");

        string date = basDt?.Length == 8 ? $"{basDt[..4]}.{basDt[4..6]}.{basDt[6..]}" : basDt;
        string sort = rule == null ? "시총 큰 순" : $"'{rule.Label}' · {SortName(rule)}";
        string hint = rule?.NeedsScreen == true && screenTable == null ? "   ※ 배당·가치 검색은 [가치 분석]을 한 번 실행하면 쓸 수 있어요" : "";
        lblMarket.Text = $"기준일 {date}  ·  {allRows.Count:N0}종목 중 {found.Count:N0}개  ·  정렬: {sort}{hint}";
    }

    // 숫자가 잘리지 않게 열마다 (머리글, 가장 긴 값) 중 넓은 쪽으로 맞추고, fillColumn이 남는 폭을 채운다
    static readonly Dictionary<string, string> ColFormat = new()
    {
        ["현재가"] = "N0", ["시가총액(억)"] = "N0", ["등락%"] = "+0.00;-0.00;0.00", ["배당%"] = "0.00",
        ["현재가(원)"] = "N0", ["평가금액"] = "N0", ["주당배당금"] = "N0", ["배당률%"] = "0.00", ["연 배당"] = "N0", ["월 배당"] = "N0", ["수량"] = "N0",
    };

    static void FitColumns(DataGridView g, DataTable t, string fillColumn)
    {
        _ = g.Handle; // 숨겨진 페이지의 표도 핸들을 먼저 만든다 (없으면 MinimumWidth 설정이 실패)
        float s = g.DeviceDpi / 96f;
        int pad = (int)(22 * s);
        foreach (DataGridViewColumn c in g.Columns)
        {
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            if (c.ValueType == typeof(double) || c.ValueType == typeof(int)) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (c.Name == fillColumn)
            {
                c.MinimumWidth = (int)(130 * s);
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                continue;
            }
            string fmt = ColFormat.GetValueOrDefault(c.Name);
            string longest = c.HeaderText;
            foreach (DataRow r in t.Rows)
            {
                var v = r[c.Name];
                string txt = v is double d && fmt != null ? d.ToString(fmt) : v?.ToString() ?? "";
                if (txt.Length > longest.Length) longest = txt;
            }
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            c.Width = Math.Max(TextRenderer.MeasureText(longest, Theme.Bold).Width, TextRenderer.MeasureText(c.HeaderText, Theme.Bold).Width) + pad;
        }
    }

    static string SortName(Labels.Rule r) => r.Label switch
    {
        "고배당" or "초고배당" => "배당수익률 높은 순",
        "꾸준배당" => "배당 연수 많은 순",
        "가치주" => "PER×PBR 낮은 순",
        "저PER" => "PER 낮은 순",
        "저PBR" => "PBR 낮은 순",
        "고ROE" or "무적자" => "ROE 높은 순",
        "성장주" => "이익성장 높은 순",
        "재무건전" => "부채비율 낮은 순",
        "그레이엄 우수" => "점수 높은 순",
        "급등" => "등락률 높은 순",
        "급락" => "등락률 낮은 순",
        "거래대금 상위" => "거래대금 큰 순",
        _ => "시총 큰 순",
    };

    void GridMarket_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        switch (gridMarket.Columns[e.ColumnIndex].Name)
        {
            case "등락%" when e.Value is double v:
                e.CellStyle.ForeColor = v > 0 ? Theme.Up : v < 0 ? Theme.Down : Theme.Muted;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
                e.CellStyle.Format = "+0.00;-0.00;0.00";
                break;
            case "현재가" or "시가총액(억)":
                e.CellStyle.Format = "N0";
                break;
            case "배당%" when e.Value is double d:
                e.CellStyle.Format = "0.00";
                if (d >= 3) e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Color.FromArgb(52, 211, 153); // 고배당 강조
                break;
            case "종목명":
                e.CellStyle.Font = Theme.Bold;
                break;
            case "종목코드" or "시장":
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Theme.Muted;
                break;
        }
    }

    void ShowDetail()
    {
        if (gridMarket.CurrentRow?.Cells["종목코드"].Value is not string code || !rowByCode.TryGetValue(code, out var r)) return;
        var q = r.Q;
        dName.Text = q.Name;
        dSub.Text = $"{q.Code}  ·  {(q.Market == "KOSPI" ? "코스피" : "코스닥")}  ·  시총 {r.Rank:0}위";
        string sign = q.Change > 0 ? "▲" : q.Change < 0 ? "▼" : "";
        dPrice.Text = $"{q.Price:N0}원   {sign}{Math.Abs(q.Change):N0} ({q.ChangeRate:+0.00;-0.00;0.00}%)";
        dPrice.ForeColor = q.Change > 0 ? Theme.Up : q.Change < 0 ? Theme.Down : Theme.Text;

        double? dps = LatestDps(code);
        if (dps > 0 && q.Price > 0)
        {
            double perTenM = 10_000_000 / q.Price * dps.Value; // 1천만원어치를 산다고 가정
            dDivCalc.Text = $"1,000만원 투자 시  연 {perTenM:N0}원 · 월 {perTenM / 12:N0}원\r\n" +
                            $"세후(15.4%)  연 {perTenM * (1 - Income.TaxRate):N0}원 · 월 {perTenM * (1 - Income.TaxRate) / 12:N0}원\r\n" +
                            $"주당 {dps:N0}원 · 현재가 기준 배당률 {dps / q.Price * 100:0.00}%" +
                            (MaybeSpecial(code) ? "\r\n⚠ 지난 결산 배당이 평소의 2배 이상 — 일회성 특별배당일 수 있어요" : "");
            dDivCalc.ForeColor = MaybeSpecial(code) ? Color.FromArgb(251, 191, 36) : Theme.Text;
        }
        else
        {
            dDivCalc.Text = dps == 0 ? "지난 결산에 현금배당이 없었어요." : "배당 정보가 아직 없어요. 담으면 DART에서 찾아봐요.";
            dDivCalc.ForeColor = Theme.Muted;
        }

        string F(double? v, string fmt, string unit = "") => v.HasValue ? v.Value.ToString(fmt) + unit : "—";
        metric["시총"].Text = $"{q.Cap / 1e12:0.0}조";
        metric["거래대금"].Text = $"{q.TradeValue / 1e8:N0}억";
        metric["PER"].Text = F(r.Per, "0.0");
        metric["PBR"].Text = F(r.Pbr, "0.00");
        metric["ROE"].Text = F(r.Roe, "0.0", "%");
        metric["배당수익률"].Text = F(r.Div, "0.0", "%");
        metric["부채비율"].Text = F(r.DebtRatio, "0", "%");
        metric["이익성장"].Text = F(r.Growth, "0", "%");
        metric["점수"].Text = r.Score?.ToString() ?? "—";
        metric["배당 연수"].Text = r.DivYears?.ToString() ?? "—";

        var s = series.GetValueOrDefault(code);
        niChart.SetData(s?.NetIncome);
        dpsChart.SetData(s?.Dps?.Where(kv => kv.Key >= 2015).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    // ================= 스크리닝 =================

    async Task RunScreen()
    {
        if (txtGo.Text.Trim() == "" || txtDart.Text.Trim() == "")
        {
            MessageBox.Show("설정에서 공공데이터포털 키와 OpenDART 키를 넣어주세요.", "키 없음");
            ShowPage(PSettings);
            return;
        }
        SaveKeys();
        btnRun.Enabled = btnCsv.Enabled = false;
        progress.Visible = true;
        var prog = new Progress<string>(s => lblStatus.Text = s);
        try
        {
            var screener = new Screener(txtGo.Text, txtDart.Text,
                (double)numPrice.Value, (double)numCap.Value * 1e12, prog);
            result = await screener.Run();
            ShowScreenResult(result, lblStatus.Text);

            Market.SaveScreen(result, screener.Series);
            screenTable = result;
            series = screener.Series;
            (basDt, quotes) = Market.LoadCached();
            if (quotes != null) RebuildRows();
        }
        catch (Exception ex)
        {
            lblStatus.Text = "오류가 났어요. 메시지를 Claude에게 보내주세요.";
            MessageBox.Show(ex.Message, "오류");
        }
        finally
        {
            btnRun.Enabled = true;
            progress.Visible = false;
        }
    }

    void ShowScreenResult(DataTable t, string status)
    {
        result = t;
        t.DefaultView.Sort = "점수 DESC, [배당수익률(%)] DESC";
        grid.DataSource = t.DefaultView;
        btnCsv.Enabled = true;
        lblStatus.Text = status;
    }

    void ExportCsv()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "screen_result.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder();
        var cols = result.Columns.Cast<DataColumn>().ToList();
        sb.AppendLine(string.Join(",", cols.Select(c => Csv(c.ColumnName))));
        foreach (DataRowView rv in result.DefaultView)
            sb.AppendLine(string.Join(",", cols.Select(c => Csv(rv[c.ColumnName]?.ToString() ?? ""))));
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); // 엑셀 한글 깨짐 방지
        lblStatus.Text = "저장 완료: " + dlg.FileName;
    }

    static string Csv(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        string col = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (e.Value is string s && (s == "O" || s == "X"))
        {
            e.CellStyle.ForeColor = s == "O" ? Theme.Good : Theme.Bad;
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (col == "점수" && e.Value is int score)
        {
            e.CellStyle.BackColor = score >= 7 ? Color.FromArgb(20, 83, 45)
                                  : score >= 5 ? Color.FromArgb(113, 63, 18)
                                  : e.CellStyle.BackColor;
            e.CellStyle.Font = boldFont ??= new Font(grid.Font, FontStyle.Bold);
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (col == "주가" || col == "시총(억)") e.CellStyle.Format = "N0";
    }

    // ================= 포트폴리오 =================

    Kiwoom GetClient()
    {
        string id = $"{rdoMock.Checked}|{txtAppKey.Text.Trim()}|{txtSecret.Text.Trim()}";
        if (kw == null || kwId != id)
        {
            kw = new Kiwoom(rdoMock.Checked, txtAppKey.Text, txtSecret.Text);
            kwId = id;
        }
        return kw;
    }

    void Log(string s) => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\r\n");

    async Task LoadPortfolio()
    {
        if (txtAppKey.Text.Trim() == "" || txtSecret.Text.Trim() == "")
        {
            MessageBox.Show($"설정에서 키움 {Mode} 앱키와 시크릿키를 넣어주세요.", "키 없음");
            ShowPage(PSettings);
            return;
        }
        SaveKwKeys();
        btnBalance.Enabled = btnOrder.Enabled = false;
        lblPf.Text = $"[{Mode}] 조회 중...";
        try
        {
            var k = GetClient();
            holdings = await k.GetHoldings();
            var (entr, ordAlow) = await k.GetCash();
            orderable = ordAlow;
            var map = Portfolio.LoadClasses();
            var cats = Portfolio.Cats.ToList();

            var cur = new double[cats.Count];
            foreach (var h in holdings) cur[cats.IndexOf(Portfolio.Classify(h.Code, h.Name, map))] += h.EvalAmt;
            totalAsset = cur.Sum() + entr;
            var alloc = Portfolio.Allocate(cur, orderable, out bool rebalancing);

            var hold = new DataTable();
            hold.Columns.Add("분류"); hold.Columns.Add("종목코드"); hold.Columns.Add("종목명");
            foreach (var c in new[] { "수량", "평균단가", "현재가", "평가금액", "비중(%)" }) hold.Columns.Add(c, typeof(double));
            foreach (var h in holdings.OrderBy(h => cats.IndexOf(Portfolio.Classify(h.Code, h.Name, map))).ThenByDescending(h => h.EvalAmt))
                hold.Rows.Add(Portfolio.Classify(h.Code, h.Name, map), h.Code, h.Name, h.Qty, h.AvgPrice, h.CurPrice, h.EvalAmt,
                    totalAsset > 0 ? h.EvalAmt / totalAsset * 100 : 0);
            gridHold.DataSource = hold;

            var sum = new DataTable();
            sum.Columns.Add("분류");
            foreach (var c in new[] { "목표(%)", "평가금액", "현재(%)", "차이(%p)", "이번 달 배분" }) sum.Columns.Add(c, typeof(double));
            var pct = new double[cats.Count];
            for (int i = 0; i < cats.Count; i++)
            {
                pct[i] = totalAsset > 0 ? cur[i] / totalAsset * 100 : 0;
                sum.Rows.Add(cats[i], Portfolio.Target[i] * 100, cur[i], pct[i], pct[i] - Portfolio.Target[i] * 100, Math.Floor(alloc[i]));
            }
            sum.Rows.Add("예수금", DBNull.Value, entr, totalAsset > 0 ? entr / totalAsset * 100 : 0, DBNull.Value, DBNull.Value);
            gridSum.DataSource = sum;
            chart.SetData(pct);

            kpiTotal.Set($"{totalAsset:N0}원");
            kpiCash.Set($"{entr:N0}원");
            kpiOrderable.Set($"{orderable:N0}원", Theme.Accent);
            kpiMode.Set(rebalancing ? "부족분 채우기" : "목표 비중대로", rebalancing ? Theme.Bad : Theme.Good);

            BuildPlan(alloc, map);
            btnOrder.Enabled = true;
            lblPf.Text = $"[{Mode}] {DateTime.Now:HH:mm} 조회 · " +
                         (rebalancing ? "±5%p 넘게 벗어난 분류가 있어 부족한 쪽에 몰아서 배분해요." : "비중이 허용 범위 안이라 목표 비중대로 배분해요.");
            Log($"[{Mode}] 잔고 {holdings.Count}종목, 예수금 {entr:N0}, 주문가능 {orderable:N0}");
        }
        catch (Exception ex)
        {
            lblPf.Text = "조회 오류. 메시지를 Claude에게 보내주세요.";
            Log("오류: " + ex.Message);
            MessageBox.Show(ex.Message, "오류");
        }
        finally
        {
            btnBalance.Enabled = true;
        }
    }

    // 채권·지수·배당: classes.txt에서 '매수' 지정한 코드, 없으면 그 분류에서 평가금액이 가장 큰 보유 종목을 산다.
    // 위성: 종목 선택은 사용자 몫이라 예산만 보여주고 빈 줄로 둔다.
    void BuildPlan(double[] alloc, Dictionary<string, Portfolio.ClassEntry> map)
    {
        plan = new DataTable();
        plan.Columns.Add("분류"); plan.Columns.Add("종목코드"); plan.Columns.Add("종목명/메모");
        plan.Columns.Add("가격", typeof(double));
        plan.Columns.Add("수량", typeof(double));
        plan.Columns.Add("금액", typeof(double), "[가격] * [수량]");
        for (int i = 0; i < Portfolio.Cats.Length; i++)
        {
            string cat = Portfolio.Cats[i];
            if (alloc[i] < 1) continue;
            if (cat == "위성")
            {
                plan.Rows.Add(cat, "", $"예산 {alloc[i]:N0}원 · 한 종목 ≤ 총자산 4%({totalAsset * Portfolio.SatelliteMax:N0}원)", 0, 0);
                continue;
            }
            string code = map.FirstOrDefault(kv => kv.Value.Cat == cat && kv.Value.BuyTarget).Key
                          ?? holdings.Where(h => Portfolio.Classify(h.Code, h.Name, map) == cat).OrderByDescending(h => h.EvalAmt).FirstOrDefault()?.Code;
            var held = holdings.FirstOrDefault(h => h.Code == code);
            double price = held?.CurPrice ?? 0;
            string memo = code == null ? $"예산 {alloc[i]:N0}원 · 종목코드 입력 또는 classes.txt에 매수 대상 지정"
                        : held == null ? $"예산 {alloc[i]:N0}원 · 가격 직접 입력"
                        : held.Name;
            plan.Rows.Add(cat, code ?? "", memo, price, price > 0 ? Math.Floor(alloc[i] / price) : 0);
        }
        gridPlan.DataSource = plan;
    }

    async Task PlaceOrders()
    {
        gridPlan.EndEdit();
        var orders = new List<(string cat, string code, long price, long qty)>();
        foreach (DataRow r in plan.Rows)
        {
            if (r.RowState == DataRowState.Deleted) continue;
            string code = (r["종목코드"] as string ?? "").Trim().ToUpperInvariant();
            long price = r["가격"] is double p ? (long)p : 0, qty = r["수량"] is double q ? (long)q : 0;
            if (qty <= 0) continue;
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9A-Z]{6}$") || price <= 0)
            {
                MessageBox.Show($"종목코드(6자리)와 가격을 확인해주세요: '{code}', {price:N0}원", "입력 확인");
                return;
            }
            orders.Add((r["분류"] as string ?? "", code, price, qty));
        }
        if (orders.Count == 0) { MessageBox.Show("수량이 1 이상인 주문이 없어요.", "주문 없음"); return; }

        double total = orders.Sum(o => (double)o.price * o.qty);
        if (total > orderable)
        {
            MessageBox.Show($"주문 합계 {total:N0}원이 주문가능금액 {orderable:N0}원보다 커요.", "금액 초과");
            return;
        }
        var warn = new List<string>();
        foreach (var o in orders.Where(o => o.cat == "위성"))
        {
            double after = (holdings.FirstOrDefault(h => h.Code == o.code)?.EvalAmt ?? 0) + o.price * o.qty;
            if (after > totalAsset * Portfolio.SatelliteMax)
                warn.Add($"⚠ {o.code} 매수 후 {after:N0}원 = 총자산의 {after / totalAsset * 100:0.0}% (한도 4%)");
        }

        var sb = new StringBuilder($"[{Mode}] 계좌로 아래 지정가 매수 주문을 보내요.\r\n\r\n");
        foreach (var o in orders) sb.AppendLine($"{o.cat}  {o.code}  {o.price:N0}원 × {o.qty:N0}주 = {o.price * o.qty:N0}원");
        sb.AppendLine($"\r\n합계 {total:N0}원 / 주문가능 {orderable:N0}원");
        foreach (var w in warn) sb.AppendLine(w);
        sb.AppendLine("\r\n접수만 되고 체결은 시장 상황에 따라 달라요. 보낼까요?");
        if (MessageBox.Show(sb.ToString(), rdoReal.Checked ? "실전 주문 확인" : "모의투자 주문 확인", MessageBoxButtons.YesNo,
                rdoReal.Checked ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        btnOrder.Enabled = btnBalance.Enabled = false;
        var k = GetClient();
        foreach (var o in orders)
        {
            try
            {
                string no = await k.Buy(o.code, o.qty, o.price);
                Log($"[{Mode}] 접수 {o.code} {o.price:N0}×{o.qty:N0} 주문번호 {no}");
            }
            catch (Exception ex)
            {
                Log($"[{Mode}] 실패 {o.code}: {ex.Message}");
            }
            await Task.Delay(300);
        }
        Log("주문 전송 끝. 체결 여부는 잠시 후 [잔고 조회 · 배분 계산]으로 확인하세요.");
        btnBalance.Enabled = true; // 중복 주문 막으려고 다시 조회해야 주문 버튼이 켜짐
    }
}
