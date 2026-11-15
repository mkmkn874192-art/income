using System.Data;
using System.Text;

namespace KrxScreen;

public class MainForm : Form
{
    static readonly string KeyFile = Path.Combine(Market.Dir, "keys.txt");
    const int PIncome = 0, PPlan = 1, PSim = 2, PMarket = 3, PSector = 4, PScreen = 5, PPortfolio = 6, PLearn = 7, PSettings = 8;

    // 제품명은 여기서만 바꾸면 된다
    public const string AppName = "인컴곳간";
    const string AppEn = "INCOME GOTGAN";
    const string Tagline = "배당·이자·적금, 들어오는 돈이 쌓이는 곳간";

    // keys.txt: 0 공공데이터, 1 DART, 2·3 키움 모의 앱키·시크릿, 4·5 키움 실전 앱키·시크릿, 6 한국은행 ECOS(선택)
    readonly string[] keys = new string[7];
    readonly TextBox txtEcos = new() { Width = 520 };

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
        PlaceholderText = "종목명 · 코드 검색, 또는 '배당주', '미국 배당주', '가치주 코스피'처럼 검색",
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
    readonly TermText dDivCalc = new() { Dock = DockStyle.Top, Height = 92, ScrollBars = RichTextBoxScrollBars.None };
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
        foreach (Control c in new Control[] { txtGo, txtDart, txtAppKey, txtSecret, txtEcos, numPrice, numCap, rdoMock, rdoReal, numAddQty }) Theme.StyleInput(c);
        rdoReal.ForeColor = Theme.Bad;
        Theme.DarkScrollbars(txtLog);
        grid.CellFormatting += Grid_CellFormatting;
        gridMarket.CellFormatting += GridMarket_CellFormatting;
        gridMarket.SelectionChanged += (_, _) => ShowDetail();
        foreach (var g in new[] { gridSum, gridHold, gridPlan }) g.DataBindingComplete += (s, _) => FormatNumbers((DataGridView)s);
        foreach (var g in new[] { grid, gridMarket, gridSum, gridHold, gridPlan, gridIncome, gridCombo, gridYears, gridSectors, gridSectorStocks })
        {
            g.DataBindingComplete += (s, _) => AddHeaderTips((DataGridView)s);
            g.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && Glossary.Find(((DataGridView)s).Columns[e.ColumnIndex].Name) is Glossary.Entry ge)
                    TermPopup.Show(ge, Cursor.Position, x => OpenLearn(x.Key));
            };
        }
        btnCsv.Enabled = btnOrder.Enabled = false;

        Controls.Add(content);
        Controls.Add(BuildSidebar());
        AddPage("", "배당 수입", BuildIncomePage());
        AddPage("", "투자 계획", BuildPlanPage());
        AddPage("", "시뮬레이터", BuildSimPage());
        AddPage("", "시장", BuildMarketPage());
        AddPage("", "섹터", BuildSectorPage());
        AddPage("", "가치 분석", BuildScreenPage());
        AddPage("", "포트폴리오", BuildPortfolioPage());
        AddPage("", "배우기", BuildLearnPage());
        AddPage("", "설정", BuildSettingsPage());

        btnSave.Click += (_, _) => { SaveKwKeys(); lblSettings.Text = "저장했어요. 다음 실행부터 자동으로 불러와요."; };
        btnRun.Click += async (_, _) => await RunScreen();
        btnCsv.Click += (_, _) => ExportCsv();
        btnBalance.Click += async (_, _) => await LoadPortfolio();
        btnOrder.Click += async (_, _) => await PlaceOrders();
        rdoReal.CheckedChanged += (_, _) => OnModeChanged();
        txtSearch.TextChanged += (_, _) => { searchDelay.Stop(); searchDelay.Start(); };
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); ApplySearch(); };
        detailDelay.Tick += async (_, _) =>
        {
            detailDelay.Stop();
            int v = detailVersion;
            await Task.WhenAll(LoadChart(detailCode, v), LoadProfile(detailCode, v), LoadDivInfo(detailCode, v));
        };

        LoadKeys();
        UpdateModeBadge();
        ShowPage(string.IsNullOrEmpty(keys[0]) || string.IsNullOrEmpty(keys[1]) ? PSettings : PIncome);
        ResumeLayout(false);

        // 열 폭 조정은 표가 화면에 만들어진 뒤에만 된다
        Shown += async (_, _) =>
        {
            RefreshIncome();
            LoadSavedData();
            refreshTimer.Tick += async (_, _) => await OnRefreshTimer();
            refreshTimer.Start();
            await OnRefreshTimer();
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

        foreach (var (label, code) in new[] { ("전체", (string)null), ("코스피", "KOSPI"), ("코스닥", "KOSDAQ"), ("미국", "US") })
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
        page.Controls.Add(Header("시장", "코스피·코스닥 전 종목과 미국 주식·ETF를 한눈에. 이름·코드는 물론 '배당주', '미국 배당주' 같은 말로도 찾을 수 있어요."));
        return page;
    }

    Control BuildDetailPanel()
    {
        string[] names = { "PER(3년)", "PER(1년)", "PBR", "ROE", "배당수익률", "부채비율", "이익성장", "점수", "배당 연수", "시총", "거래대금", "" };
        for (int i = 0; i < 4; i++) dMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        foreach (var n in names)
        {
            var name = new Label { Text = n, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 6, 4, 0) };
            if (Glossary.Find(n) is Glossary.Entry ge)
            {
                tips.SetToolTip(name, ge.Short + "\n(클릭하면 자세한 설명)");
                name.Cursor = Cursors.Help;
                name.Click += (_, _) => TermPopup.Show(ge, Cursor.Position, e => OpenLearn(e.Key));
            }
            dMetrics.Controls.Add(name);
            var v = new Label { Text = "—", AutoSize = true, Font = Theme.Bold, ForeColor = Theme.Text, Margin = new Padding(0, 5, 8, 0) };
            dMetrics.Controls.Add(v);
            metric[n] = v;
        }

        var inner = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        Theme.DarkScrollbars(inner);
        Label Section(string t) => new() { Text = t, Dock = DockStyle.Top, Height = 32, Font = Theme.H2, ForeColor = Theme.Text, Padding = new Padding(0, 12, 0, 0) };
        // Dock=Top은 나중에 추가한 것이 위로 간다 → 아래쪽부터 추가
        var addRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        addRow.Controls.AddRange(new Control[] { numAddQty, Theme.Text_("주"), btnAdd });
        btnAdd.Click += async (_, _) => await AddToIncome();

        var ranges = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 4) };
        foreach (var (label, range) in new[] { ("1개월", "1mo"), ("1년", "1y"), ("5년", "5y") })
        {
            var p = new Pill(label, Theme.Text, clickable: true) { Selected = range == chartRange };
            p.Click += async (_, _) =>
            {
                chartRange = range;
                foreach (Pill x in ranges.Controls) { x.Selected = x == p; x.Invalidate(); }
                await LoadChart(detailCode, ++detailVersion);
            };
            ranges.Controls.Add(p);
        }

        inner.Controls.Add(dpsChart);
        inner.Controls.Add(Section("주당배당금 추이 (원)"));
        inner.Controls.Add(niChart);
        inner.Controls.Add(Section("순이익 추이 (억원)"));
        inner.Controls.Add(addRow);
        inner.Controls.Add(dDivInfo);
        inner.Controls.Add(dDivCalc);
        inner.Controls.Add(Section("배당으로 받을 돈"));
        inner.Controls.Add(dMetrics);
        inner.Controls.Add(Section("핵심 지표"));
        inner.Controls.Add(dProfile);
        inner.Controls.Add(Section("회사 소개"));
        inner.Controls.Add(dCompare);
        inner.Controls.Add(Section("시장·업종 대비"));
        inner.Controls.Add(priceChart);
        inner.Controls.Add(ranges);
        inner.Controls.Add(dPrice);
        inner.Controls.Add(dSub);
        inner.Controls.Add(dName);
        return new Card(null, inner) { Dock = DockStyle.Right, Width = 460, Margin = Padding.Empty, Padding = new Padding(18, 14, 8, 14) };
    }

    // ----- 상세: 시세 그래프·회사 소개 (선택이 바뀔 때마다 Yahoo·DART에서 받아 캐시) -----
    readonly PriceChart priceChart = new();
    readonly TermText dCompare = new() { Dock = DockStyle.Top, Height = 110, ScrollBars = RichTextBoxScrollBars.None };
    readonly TermText dDivInfo = new() { Dock = DockStyle.Top, Height = 92, ScrollBars = RichTextBoxScrollBars.None };
    readonly Label dProfile = new() { Dock = DockStyle.Top, Height = 170, Font = Theme.Small, ForeColor = Theme.Text, Padding = new Padding(0, 2, 8, 0) };
    readonly System.Windows.Forms.Timer detailDelay = new() { Interval = 250 }; // 방향키로 훑을 때 매번 받지 않게
    readonly Dictionary<string, List<Yahoo.Point>> chartCache = new();
    readonly Dictionary<string, string> profileCache = new();
    string chartRange = "1y", detailCode;
    int detailVersion;

    async Task LoadChart(string code, int version)
    {
        if (code == null || !rowByCode.TryGetValue(code, out var r)) return;
        string key = Yahoo.Symbol(r.Q) + "|" + chartRange;
        if (!chartCache.TryGetValue(key, out var pts))
        {
            priceChart.SetMessage("시세 기록을 받는 중...");
            try { pts = chartCache[key] = await Yahoo.Chart(Yahoo.Symbol(r.Q), chartRange); }
            catch (Exception ex) { if (version == detailVersion) priceChart.SetMessage("시세 기록을 못 받았어요: " + ex.Message); return; }
        }
        if (version == detailVersion) priceChart.SetData(pts, r.Q.IsUs); // 그 사이 다른 종목을 골랐으면 버린다
    }

    // 배당 기록이 아직 없는 종목은 고를 때 받아서 점수까지 다시 계산
    async Task LoadDivInfo(string code, int version)
    {
        if (code == null || !rowByCode.TryGetValue(code, out var r) || r.DivI != null) return;
        try
        {
            r.DivI = await Dividends.Fetch(Yahoo.Symbol(r.Q));
            Score.Compute(r, r.Sec, r.DivI);
            Labels.Enrich(r);
        }
        catch { return; }
        if (version == detailVersion) FillDivInfo(r);
    }

    async Task LoadProfile(string code, int version)
    {
        if (!rowByCode.TryGetValue(code, out var r)) return;
        if (!profileCache.TryGetValue(code, out var text))
        {
            dProfile.Text = "회사 정보를 받는 중...";
            var sb = new StringBuilder();
            try
            {
                if (!r.Q.IsUs && !string.IsNullOrWhiteSpace(keys[1]))
                {
                    var c = await Income.CompanyInfo(keys[1], code);
                    if (c != null)
                    {
                        string est = c.Established?.Length == 8 ? $"{c.Established[..4]}.{c.Established[4..6]}.{c.Established[6..]}" : c.Established;
                        sb.AppendLine($"대표 {c.Ceo}  ·  설립 {est}" + (string.IsNullOrEmpty(c.Homepage) ? "" : $"  ·  {c.Homepage}"));
                    }
                }
                var p = await Yahoo.GetProfile(Yahoo.Symbol(r.Q));
                if (p != null)
                {
                    var head = new List<string>();
                    if (p.Sector != null) head.Add($"{Yahoo.SectorKo(p.Sector)} · {p.Industry}");
                    if (p.Category != null) head.Add($"펀드 유형: {p.Category}");
                    if (p.Employees > 0) head.Add($"직원 {p.Employees:N0}명");
                    if (r.Q.IsUs && p.Website != null) head.Add(p.Website);
                    if (head.Count > 0) sb.AppendLine(string.Join("  ·  ", head));
                    if (!string.IsNullOrWhiteSpace(p.Summary))
                        sb.Append(p.Summary.Length > 520 ? p.Summary[..520].TrimEnd() + "…" : p.Summary);
                }
                text = profileCache[code] = sb.Length > 0 ? sb.ToString() : "회사 정보가 없어요.";
            }
            catch (Exception ex)
            {
                text = "회사 정보를 못 받았어요: " + ex.Message.Split('\n')[0];
            }
        }
        if (version == detailVersion) dProfile.Text = text;
    }

    Control BuildScreenPage()
    {
        var page = Page();
        page.Controls.Add(new Card(null, grid) { Margin = Padding.Empty, Padding = new Padding(1) });
        page.Controls.Add(Theme.Spacer(14));
        page.Controls.Add(Toolbar(Theme.Text_("주가 하한(원)"), numPrice, Theme.Text_("   시총 하한(조)"), numCap, Theme.Text_("   "),
            btnRun, btnCsv, progress, lblStatus));
        page.Controls.Add(Header("가치 분석", "10년 재무와 배당 이력을 『현명한 투자자』의 7가지 기준으로 채점해요. PER은 책대로 최근 3년 평균 이익으로 계산해요(15배 이하 통과)."));
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
        Row("한국은행 ECOS 키", txtEcos, "선택. 비워 두면 체험용 키로 국고채·기준금리를 받아요. ecos.bok.or.kr → 인증키 신청");
        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.AddRange(new Control[] { btnSave, lblSettings });
        form.Controls.Add(new Label());
        form.Controls.Add(actions);

        var card = new Card("API 키", form) { Dock = DockStyle.Top, Height = 560, Margin = Padding.Empty };

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
        txtEcos.Text = keys[6];
        ShowKwKeys();
    }

    void SaveKeys()
    {
        keys[0] = txtGo.Text.Trim();
        keys[1] = txtDart.Text.Trim();
        keys[6] = txtEcos.Text.Trim();
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
            if (n is "현재가" or "주당배당금" && gridIncome.Rows[e.RowIndex].Cells["통화"].Value as string == "USD") e.CellStyle.Format = "$#,##0.00";
            if (n is "수량" or "현재가" or "주당배당금") e.CellStyle.BackColor = Theme.InputBg; // 고칠 수 있는 칸
            if (n is "연 배당(원)" or "월 배당(원)") { e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153); e.CellStyle.Font = Theme.Bold; }
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
        // 현재가·주당배당금은 그 나라 통화, 금액 열은 원화 (미국은 현재 환율로 환산)
        incomeTable = new DataTable();
        incomeTable.Columns.Add("종목명"); incomeTable.Columns.Add("코드"); incomeTable.Columns.Add("통화");
        foreach (var c in new[] { "수량", "현재가", "주당배당금", "배당률%", "평가금액(원)", "연 배당(원)", "월 배당(원)" })
            incomeTable.Columns.Add(c, typeof(double));
        incomeTable.Columns.Add("출처");
        foreach (var i in incomeItems)
        {
            double fx = Market.Fx(i.Currency), keep = afterTax ? 1 - Income.Tax(i.Currency) : 1;
            incomeTable.Rows.Add(i.Name, i.Code, i.Currency, i.Qty, i.Price, i.Dps, i.Price > 0 ? i.Dps / i.Price * 100 : 0,
                i.Qty * i.Price * fx, i.Qty * i.Dps * fx * keep, i.Qty * i.Dps * fx * keep / 12, i.Source);
        }
        incomeTable.ColumnChanged += (_, e) =>
        {
            var item = incomeItems.FirstOrDefault(i => i.Code == (e.Row["코드"] as string));
            if (item == null) return;
            double v = e.ProposedValue is double d ? d : 0;
            if (e.Column.ColumnName == "수량") item.Qty = v;
            else if (e.Column.ColumnName == "현재가") item.Price = v;
            else if (e.Column.ColumnName == "주당배당금") { item.Dps = v; item.Source = "직접 입력"; }
            else return;
            Income.Save(incomeItems);
            BeginInvoke(new Action(RefreshIncome)); // 편집이 끝난 뒤 합계 다시 계산
        };
        gridIncome.DataSource = incomeTable;
        foreach (DataGridViewColumn c in gridIncome.Columns)
            c.ReadOnly = c.Name is not ("수량" or "현재가" or "주당배당금");
        FitColumns(gridIncome, incomeTable, "종목명");

        double annual = incomeItems.Sum(i => i.Qty * i.Dps * Market.Fx(i.Currency) * (afterTax ? 1 - Income.Tax(i.Currency) : 1));
        double invested = incomeItems.Sum(i => i.Qty * i.Price * Market.Fx(i.Currency));
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
            : $"{incomeItems.Count}종목 · 투자금 {Krw(invested)} · 평균 배당률 {yield * 100:0.00}% ({tax})" +
              (incomeItems.Any(i => i.Currency == "USD") ? $" · 환율 {Market.UsdKrw:N1}원" : "") +
              "  ·  추정치예요. 실제 배당은 회사 결정에 따라 달라지고, 월·주·일 금액은 1년치를 고르게 나눈 평균이에요. 세후: 국내 15.4%, 미국 15%";
    }

    // 스크리닝 때 모은 가장 최근 연도의 주당배당금
    double? LatestDps(string code)
    {
        if (rowByCode.TryGetValue(code, out var row) && row.Q.IsUs) return row.Q.Dps; // 미국: Yahoo 배당률 기준
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
        if (rowByCode.TryGetValue(code, out var row) && row.Q.IsUs)
            return (row.Q.Dps ?? 0, row.Q.Dps > 0 ? $"Yahoo 배당률 {row.Q.DivYield:0.00}%" : "직접 입력 필요");
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
                item = new IncomeItem { Code = code, Name = r.Q.Name, Dps = dps, Source = src, Currency = r.Q.Currency };
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

    // ================= 시뮬레이터 =================

    readonly DataGridView gridSimAssets = NewGrid(false);
    readonly NumericUpDown numSimStart = new() { Minimum = 0, Maximum = 10_000_000, Value = 1000, Width = 90, ThousandsSeparator = true };
    readonly NumericUpDown numSimMonthly = new() { Minimum = 0, Maximum = 100_000, Value = 100, Width = 80, ThousandsSeparator = true };
    readonly CheckBox chkSimRebal = new() { Text = "매년 리밸런싱", AutoSize = true, Checked = true, Margin = new Padding(12, 8, 0, 0) };
    readonly CheckBox chkSimReinvest = new() { Text = "배당 재투자", AutoSize = true, Checked = true, Margin = new Padding(12, 8, 0, 0) };
    readonly Button btnSimRun = Theme.Primary("시뮬레이션 실행");
    readonly KpiTile kSimFinal = new("최종 평가금액"), kSimGain = new("수익"), kSimCagr = new("연평균 수익률"), kSimDiv = new("받은 배당 (세후)"), kSimMdd = new("최대 낙폭");
    readonly SimChart simChart = new() { Dock = DockStyle.Fill };
    readonly DataGridView gridSimPeriods = NewGrid(true);
    readonly DataGridView gridSimAmounts = NewGrid(true);
    readonly TermText txtSimRisk = new() { Dock = DockStyle.Fill };
    readonly Label lblSim = Theme.Text_("과거 월말 종가와 실제 배당 기록(Yahoo)으로 계산해요. 과거 결과가 미래를 보장하지 않아요.", Theme.Small, Theme.Muted);
    readonly Dictionary<string, SortedDictionary<DateTime, (double close, double div)>> simCache = new();
    int simMonths = 120;

    Control BuildSimPage()
    {
        var page = Page();
        foreach (var g in new[] { gridSimAssets, gridSimPeriods, gridSimAmounts })
        {
            Theme.StyleGrid(g);
            g.DataBindingComplete += (s, _) => FormatNumbers((DataGridView)s);
        }
        foreach (Control c in new Control[] { numSimStart, numSimMonthly, chkSimRebal, chkSimReinvest }) Theme.StyleInput(c);
        txtSimRisk.OpenFull = e => OpenLearn(e.Key);
        SetSimAssets(DefaultSimAssets());

        var btnFromPlan = Theme.Secondary("투자 계획 조합 불러오기");
        var btnFromIncome = Theme.Secondary("배당 목록 불러오기");
        btnFromPlan.Click += (_, _) =>
        {
            if (picks.Count == 0) { lblSim.Text = "[투자 계획]에서 먼저 [조합표 만들기]를 눌러주세요."; return; }
            SetSimAssets(picks.Select(p => (p.Code, p.Name, p.Weight * 100)).ToList());
        };
        btnFromIncome.Click += (_, _) =>
        {
            double total = incomeItems.Sum(i => i.Qty * i.Price * Market.Fx(i.Currency));
            if (total <= 0) { lblSim.Text = "[배당 수입]에 담긴 종목이 없어요."; return; }
            SetSimAssets(incomeItems.Select(i => (i.Code, i.Name, i.Qty * i.Price * Market.Fx(i.Currency) / total * 100)).ToList());
        };
        btnSimRun.Click += async (_, _) => await RunSim();

        var periodBar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(16, 0, 0, 0) };
        var pills = new List<Pill>();
        foreach (var (label, months) in new[] { ("1년", 12), ("3년", 36), ("5년", 60), ("10년", 120) })
        {
            var p = new Pill(label, Theme.Text, clickable: true) { Selected = months == simMonths, Margin = new Padding(0, 6, 6, 0) };
            p.Click += async (_, _) =>
            {
                simMonths = months;
                foreach (var x in pills) { x.Selected = x == p; x.Invalidate(); }
                if (gridSimPeriods.DataSource != null) await RunSim();
            };
            pills.Add(p);
            periodBar.Controls.Add(p);
        }

        var row1 = Toolbar(Theme.Text_("시작 금액"), numSimStart, Theme.Text_("만원    매달"), numSimMonthly, Theme.Text_("만원    기간"), periodBar,
            chkSimRebal, chkSimReinvest);
        var row2 = Toolbar(btnFromPlan, btnFromIncome, btnSimRun);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, ColumnCount = 5, Margin = Padding.Empty };
        for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        kpis.Controls.AddRange(new Control[] { kSimFinal, kSimGain, kSimCagr, kSimDiv, kSimMdd });

        var tables = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        tables.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        tables.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        tables.Controls.Add(new Card("기간별로 들고 있었다면", gridSimPeriods), 0, 0);
        tables.Controls.Add(new Card("매달 얼마씩 넣었다면 (선택한 기간)", gridSimAmounts), 0, 1);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        bottom.Controls.Add(tables, 0, 0);
        bottom.Controls.Add(new Card("리스크와 운용 가능한 금액", txtSimRisk), 1, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        kpis.Dock = DockStyle.Fill;
        right.Controls.Add(kpis, 0, 0);
        right.Controls.Add(new Card("평가금액 흐름", simChart), 0, 1);
        right.Controls.Add(bottom, 0, 2);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        body.Controls.Add(new Card("구성 · 종목코드와 비중(%) 직접 수정 가능", gridSimAssets), 0, 0);
        body.Controls.Add(right, 1, 0);

        var status = new Panel { Dock = DockStyle.Top, Height = 26 };
        lblSim.Dock = DockStyle.Fill;
        status.Controls.Add(lblSim);

        page.Controls.Add(body);
        page.Controls.Add(status);
        page.Controls.Add(Theme.Spacer(6));
        page.Controls.Add(row2);
        page.Controls.Add(Theme.Spacer(6));
        page.Controls.Add(row1);
        page.Controls.Add(Header("시뮬레이터", "이 구성으로 1·3·5·10년 전에 사서 매달 적립했다면 지금 얼마가 됐을지, 실제 과거 가격으로 계산해요."));
        return page;
    }

    // 기본 구성: 투자 계획의 ETF 기본 추천 (위성은 빼고 비중 다시 맞춤)
    List<(string code, string name, double weight)> DefaultSimAssets() =>
        Enumerable.Range(0, 3).Select(i => (Plan.Etfs[Portfolio.Cats[i]][0].Code, Plan.Etfs[Portfolio.Cats[i]][0].Name, Portfolio.Target[i] * 100)).ToList();

    void SetSimAssets(List<(string code, string name, double weight)> items)
    {
        var t = new DataTable();
        t.Columns.Add("종목코드"); t.Columns.Add("종목명");
        t.Columns.Add("비중%", typeof(double));
        foreach (var (code, name, weight) in items) t.Rows.Add(code, name, Math.Round(weight, 1));
        gridSimAssets.DataSource = t;
    }

    // 종목코드 → Yahoo 심볼·이름·대용 ETF. 6자리면 국장(ETF는 코스피 상장), 아니면 미국.
    (string sym, string name, string proxy) SimSymbol(string code)
    {
        code = code.Trim().ToUpperInvariant();
        var etf = Plan.Etfs.Values.SelectMany(a => a).FirstOrDefault(e => e.Code == code);
        if (rowByCode.TryGetValue(code, out var r)) return (Yahoo.Symbol(r.Q), r.Q.Name, etf?.Proxy);
        if (code.Length == 6 && code.All(char.IsLetterOrDigit) && code.Any(char.IsDigit)) return (code + ".KS", etf?.Name ?? code, etf?.Proxy);
        return (code, code, null);
    }

    async Task RunSim()
    {
        gridSimAssets.EndEdit();
        var assets = new List<Backtest.Asset>();
        foreach (DataRow row in ((DataTable)gridSimAssets.DataSource).Rows)
        {
            if (row.RowState == DataRowState.Deleted || row["종목코드"] is not string code || code.Trim() == "") continue;
            double w = row["비중%"] is double d ? d : 0;
            if (w <= 0) continue;
            var (sym, name, proxy) = SimSymbol(code);
            assets.Add(new Backtest.Asset(sym, row["종목명"] as string is { Length: > 0 } n ? n : name, w, proxy));
        }
        if (assets.Count == 0) { lblSim.Text = "구성에 종목코드와 비중을 넣어주세요."; return; }

        btnSimRun.Enabled = false;
        lblSim.Text = "과거 가격과 배당 기록을 받는 중...";
        try
        {
            // 받은 자료는 기억해 두고 다시 쓰지 않는다
            var need = assets.SelectMany(a => new[] { a.Symbol, a.Proxy }).Where(s => s != null).Append("KRW=X").Distinct().Where(s => !simCache.ContainsKey(s)).ToList();
            foreach (var s in need) simCache[s] = await Yahoo.Monthly(s);
            var data = simCache;

            double start = (double)numSimStart.Value * 1e4, monthly = (double)numSimMonthly.Value * 1e4;
            bool rebal = chkSimRebal.Checked, reinvest = chkSimReinvest.Checked;
            var main = Backtest.Run(assets, data, simMonths, start, monthly, rebal, reinvest);
            simChart.SetData(main.Series);

            double gain = main.Final - main.Principal;
            kSimFinal.Set(Krw(main.Final), null, $"넣은 원금 {Krw(main.Principal)}");
            kSimGain.Set($"{gain / 1e4:+#,0;-#,0}만원", gain >= 0 ? Theme.Up : Theme.Down, $"원금 대비 {gain / Math.Max(1, main.Principal) * 100:+0.0;-0.0}%");
            kSimCagr.Set($"{main.Cagr:+0.0;-0.0}%", main.Cagr >= 0 ? Theme.Up : Theme.Down, "적립 시점과 무관한 연평균");
            kSimDiv.Set(Krw(main.DivTotal), Color.FromArgb(52, 211, 153), $"최근 1년 {Krw(main.Last12Div)} (월 {Krw(main.Last12Div / 12)})");
            kSimMdd.Set($"{main.Mdd:0.0}%", Theme.Down, double.IsNaN(main.WorstYear) ? "1년 넘게 보유해야 '최악의 1년'을 계산해요" : $"최악의 1년 {main.WorstYear:+0.0;-0.0}%");

            // 기간별
            var pt = new DataTable();
            pt.Columns.Add("기간");
            foreach (var c in new[] { "연평균%", "최대낙폭%", "최악1년%", "넣은 원금", "최종 평가", "받은 배당" }) pt.Columns.Add(c, typeof(double));
            foreach (var (label, m) in new[] { ("1년", 12), ("3년", 36), ("5년", 60), ("10년", 120) })
            {
                var r = Backtest.Run(assets, data, m, start, monthly, rebal, reinvest);
                pt.Rows.Add(label, r.Cagr, r.Mdd, double.IsNaN(r.WorstYear) ? DBNull.Value : r.WorstYear, r.Principal, r.Final, r.DivTotal);
            }
            gridSimPeriods.DataSource = pt;

            // 적립액별
            var at = new DataTable();
            foreach (var c in new[] { "월 적립(만원)", "넣은 원금", "최종 평가", "수익", "받은 배당", "월평균 배당(최근1년)" }) at.Columns.Add(c, typeof(double));
            foreach (var mo in new[] { 0, 30, 50, 100, 200, 300 })
            {
                var r = Backtest.Run(assets, data, simMonths, start, mo * 1e4, rebal, reinvest);
                at.Rows.Add(mo, r.Principal, r.Final, r.Final - r.Principal, r.DivTotal, r.Last12Div / 12);
            }
            gridSimAmounts.DataSource = at;

            txtSimRisk.SetText(SimRiskText(assets, data, main, start, rebal, reinvest));
            lblSim.Text = $"{main.Series[0].Month:yyyy.MM} ~ {main.Series[^1].Month:yyyy.MM} · 월말 종가 기준, 소수점 수량으로 산다고 가정 · 배당은 세후(국내 15.4%, 미국 15%) · 과거 결과가 미래를 보장하지 않아요.";
        }
        catch (Exception ex)
        {
            lblSim.Text = "시뮬레이션을 못 했어요: " + ex.Message.Split('\n')[0];
        }
        finally
        {
            btnSimRun.Enabled = true;
        }
    }

    string SimRiskText(List<Backtest.Asset> assets, Dictionary<string, SortedDictionary<DateTime, (double close, double div)>> data,
                       Backtest.Result main, double start, bool rebal, bool reinvest)
    {
        var sb = new StringBuilder();
        double basis = Math.Max(start, 1e7);
        sb.AppendLine("■ 과거 가장 나빴던 순간");
        sb.AppendLine($"최대 낙폭 {main.Mdd:0.0}% — {Krw(basis)}이었다면 한때 {Krw(basis * (1 + main.Mdd / 100))}까지 줄었던 셈이에요.");
        if (!double.IsNaN(main.WorstYear))
            sb.AppendLine(main.WorstYear < 0
                ? $"최악의 1년 수익률 {main.WorstYear:0.0}%. 이 정도 하락을 보고도 팔지 않고 버틸 수 있는지가 핵심이에요."
                : $"어느 1년을 잘라 봐도 플러스였어요(가장 낮은 1년 {main.WorstYear:+0.0}%). 다만 다음 기간도 그렇다는 보장은 없어요.");
        sb.AppendLine();

        // 채권 비중을 20%p 늘리면? (국채 ETF가 구성에 있을 때)
        int bond = assets.FindIndex(a => a.Name.Contains("국채") || a.Name.Contains("국고채"));
        if (bond >= 0)
        {
            double total = assets.Sum(a => a.Weight), bw = assets[bond].Weight / total;
            if (bw < 0.85)
            {
                double nb = Math.Min(0.95, bw + 0.2), scale = (1 - nb) / (1 - bw);
                var safer = assets.Select((a, i) => a with { Weight = i == bond ? nb : a.Weight / total * scale }).ToList();
                var r = Backtest.Run(safer, data, simMonths, start, (double)numSimMonthly.Value * 1e4, rebal, reinvest);
                sb.AppendLine($"■ 채권을 {bw * 100:0}% → {nb * 100:0}%로 늘리면");
                sb.AppendLine($"최대 낙폭 {main.Mdd:0.0}% → {r.Mdd:0.0}%, 연평균 {main.Cagr:+0.0;-0.0}% → {r.Cagr:+0.0;-0.0}%. 덜 흔들리는 대신 덜 벌어요.");
                sb.AppendLine();
            }
        }

        // 1주 단위로 살 때 최소 월 적립액
        var fx = data["KRW=X"];
        var prices = assets.Select(a =>
        {
            var d = data.GetValueOrDefault(a.Symbol);
            if (d == null || d.Count == 0) return 0;
            double p = d.Last().Value.close;
            return Backtest.IsKrw(a.Symbol) ? p : p * fx.Last().Value.close;
        }).ToArray();
        if (prices.All(p => p > 0))
        {
            var (min, slow) = Backtest.MinMonthly(assets, prices, Portfolio.Band);
            sb.AppendLine("■ 운용 가능한 최소 월 적립액");
            if (double.IsNaN(min)) sb.AppendLine("1천만원 안에서는 비중을 맞추기 어려워요. 종목 수를 줄이거나 1주 가격이 낮은 상품을 고르세요.");
            else
            {
                sb.AppendLine($"매달 약 {Krw(min)} 이상이면 1주 단위로 사도 비중이 목표 ±{Portfolio.Band * 50:0.#}%p 안에서 유지돼요.");
                foreach (var s in slow) sb.AppendLine("• " + s);
            }
            sb.AppendLine();
        }

        sb.AppendLine("■ 리스크를 줄이는 기본");
        sb.AppendLine("• 생활비 3~6개월치 비상금은 투자 밖에 두기 — 급할 때 하락장에 팔지 않으려면");
        sb.AppendLine("• 한 번에 넣기보다 매달 나눠 넣기(적립식)");
        sb.AppendLine("• 리밸런싱으로 많이 오른 쪽을 덜 사고 덜 오른 쪽을 더 사기");
        sb.AppendLine("• 한 종목은 전체의 4% 이하");
        if (main.Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("■ 참고");
            foreach (var n in main.Notes) sb.AppendLine("• " + n);
        }
        return sb.ToString();
    }

    // ================= 섹터 =================

    readonly DataGridView gridSectors = NewGrid(true);
    readonly DataGridView gridSectorStocks = NewGrid(true);
    readonly CheckBox chkStable = new() { Text = "배당 안정 종목만", AutoSize = true, Margin = new Padding(16, 8, 0, 0) };
    readonly Label lblSector = Theme.Text_("", Theme.Small, Theme.Muted);
    readonly Label lblSectorPick = new() { Dock = DockStyle.Top, Height = 26, Font = Theme.H2, ForeColor = Theme.Text };
    string sectorRegion = "국장", sectorPick;

    Control BuildSectorPage()
    {
        var page = Page();
        foreach (var g in new[] { gridSectors, gridSectorStocks })
        {
            Theme.StyleGrid(g);
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.CellFormatting += SectorGrid_CellFormatting;
        }
        Theme.StyleInput(chkStable);
        gridSectors.SelectionChanged += (_, _) =>
        {
            if (gridSectors.CurrentRow?.Cells["업종"].Value is string ko) { sectorPick = ko; FillSectorStocks(); }
        };
        gridSectorStocks.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && gridSectorStocks.Rows[e.RowIndex].Cells["종목코드"].Value is string code)
            {
                txtSearch.Text = code;
                ShowPage(PMarket);
            }
        };
        chkStable.CheckedChanged += (_, _) => FillSectorStocks();

        var pills = new List<Pill>();
        var bar = Toolbar();
        foreach (var region in new[] { "국장", "미장" })
        {
            var p = new Pill(region, Theme.Text, clickable: true) { Selected = region == sectorRegion, Margin = new Padding(0, 6, 6, 0) };
            p.Click += (_, _) =>
            {
                sectorRegion = region;
                foreach (var x in pills) { x.Selected = x == p; x.Invalidate(); }
                sectorPick = null;
                RefreshSectors();
            };
            pills.Add(p);
            bar.Controls.Add(p);
        }
        bar.Controls.Add(chkStable);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(gridSectorStocks);
        gridSectorStocks.Dock = DockStyle.Fill;
        right.Controls.Add(lblSectorPick);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        body.Controls.Add(new Card("업종 강도  ·  단기 = 50일선 대비, 중장기 = 200일선 대비 (업종 안 종목 중앙값)", gridSectors), 0, 0);
        body.Controls.Add(new Card("관련 종목  ·  종합 점수 순, 두 번 클릭하면 시장 화면에서 자세히", right), 1, 0);

        var status = new Panel { Dock = DockStyle.Top, Height = 26 };
        lblSector.Dock = DockStyle.Fill;
        status.Controls.Add(lblSector);

        page.Controls.Add(body);
        page.Controls.Add(status);
        page.Controls.Add(bar);
        page.Controls.Add(Header("섹터", "업종마다 단기·중장기 흐름과 국면을 보고, 그 업종에서 배당이 안정적이고 종합 점수가 높은 종목을 찾아요."));
        return page;
    }

    void RefreshSectors()
    {
        var list = sectorStats.Where(s => s.Region == sectorRegion).OrderByDescending(s => s.Vs200 + s.Vs50).ToList();
        var t = new DataTable();
        t.Columns.Add("업종"); t.Columns.Add("국면");
        t.Columns.Add("종목수", typeof(int));
        foreach (var c in new[] { "단기%", "중장기%", "1년%", "고점대비%", "평균배당%" }) t.Columns.Add(c, typeof(double));
        foreach (var s in list)
            t.Rows.Add(Yahoo.SectorKo(s.Sector), s.Phase, s.N, s.Vs50, s.Vs200, s.Chg1y, s.FromHigh, s.AvgYield);
        gridSectors.DataSource = t;
        if (gridSectors.Columns.Count > 0) FitColumns(gridSectors, t, "업종");
        lblSector.Text = list.Count == 0
            ? "업종 자료가 아직 없어요. 시세 갱신(30분마다) 뒤에 보여요."
            : $"{sectorRegion} {list.Count}개 업종 · 국면: 강세 지속(단기·중장기 모두 +) / 반등 시작(단기만 +) / 조정 중(중장기만 +) / 약세(모두 −) · 과거 흐름이지 예측이 아니에요.";
        FillSectorStocks();
    }

    void FillSectorStocks()
    {
        var sec = sectorStats.FirstOrDefault(s => s.Region == sectorRegion && Yahoo.SectorKo(s.Sector) == sectorPick);
        if (sec == null) { gridSectorStocks.DataSource = null; lblSectorPick.Text = "왼쪽에서 업종을 고르세요"; return; }
        var rows = allRows.Where(r => Score.Region(r.Q) == sectorRegion && r.Q.Sector == sec.Sector)
                          .Where(r => !chkStable.Checked || Labels.IsStable(r))
                          .OrderByDescending(r => r.Total ?? -1).ThenByDescending(r => r.Q.Cap).Take(300).ToList();
        var t = new DataTable();
        t.Columns.Add("종목코드"); t.Columns.Add("종목명");
        foreach (var c in new[] { "시가총액(억)", "배당%" }) t.Columns.Add(c, typeof(double));
        t.Columns.Add("배당주기");
        t.Columns.Add("연속배당", typeof(int));
        foreach (var c in new[] { "1년%", "고점대비%", "종합점수" }) t.Columns.Add(c, typeof(double));
        object N(double? v) => v.HasValue ? v.Value : DBNull.Value;
        foreach (var r in rows)
            t.Rows.Add(r.Q.Code, r.Q.Name, Math.Round(r.Q.Cap / 1e8), N(r.Yield ?? r.Div),
                r.DivI == null ? "" : r.DivI.Freq == "없음" ? "-" : r.DivI.Freq,
                r.DivI != null ? r.DivI.Years : r.DivYears.HasValue ? r.DivYears.Value : DBNull.Value,
                N(r.Q.Chg1y), N(r.Q.FromHigh), N(r.Total));
        gridSectorStocks.DataSource = t;
        FitColumns(gridSectorStocks, t, "종목명");
        lblSectorPick.Text = $"{sectorPick} · {sec.Phase} · {rows.Count}종목" + (chkStable.Checked ? " (배당 안정만)" : "");
    }

    void SectorGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var g = (DataGridView)sender;
        string n = g.Columns[e.ColumnIndex].Name;
        if (e.Value is double v)
        {
            if (n is "단기%" or "중장기%" or "1년%")
            {
                e.CellStyle.Format = "+0.0;-0.0;0.0";
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = v > 0 ? Theme.Up : v < 0 ? Theme.Down : Theme.Muted;
            }
            else if (n is "고점대비%" or "평균배당%" or "배당%") e.CellStyle.Format = n == "고점대비%" ? "0.0" : "0.00";
            else if (n == "종합점수")
            {
                e.CellStyle.Format = "0";
                e.CellStyle.Font = Theme.Bold;
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor =
                    v >= 70 ? Color.FromArgb(52, 211, 153) : v >= 50 ? Color.FromArgb(251, 191, 36) : Theme.Muted;
            }
            else e.CellStyle.Format = "N0";
        }
        if (n == "국면" && e.Value is string ph)
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = ph switch
            {
                "강세 지속" => Theme.Up, "반등 시작" => Color.FromArgb(251, 191, 36), "조정 중" => Color.FromArgb(167, 139, 250), _ => Theme.Down,
            };
    }

    // ================= 배우기 =================

    readonly ListBox lstLearn = new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.CardBg, ForeColor = Theme.Text,
        DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false,
    };
    readonly RichTextBox rtbLearn = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.CardBg, ForeColor = Theme.Text };
    readonly TextBox txtLearn = new() { Dock = DockStyle.Top, PlaceholderText = "용어 검색 (예: PER, 국채, 배당)", Font = Theme.Body };
    readonly ToolTip tips = new() { AutoPopDelay = 15000, InitialDelay = 300 };

    Control BuildLearnPage()
    {
        var page = Page();
        Theme.StyleInput(txtLearn);
        Theme.DarkScrollbars(rtbLearn);
        Theme.DarkScrollbars(lstLearn);
        lstLearn.ItemHeight = (int)(30 * DeviceDpi / 96f);
        lstLearn.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var item = (Glossary.Entry)lstLearn.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? Theme.AccentSoft : Theme.CardBg);
            e.Graphics.FillRectangle(bg, e.Bounds);
            TextRenderer.DrawText(e.Graphics, item.Title, sel ? Theme.Bold : Theme.Body, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        lstLearn.SelectedIndexChanged += (_, _) => { if (lstLearn.SelectedItem is Glossary.Entry en) ShowLearn(en); };
        txtLearn.TextChanged += (_, _) => FillLearnList();

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(lstLearn);
        left.Controls.Add(Theme.Spacer(8));
        left.Controls.Add(txtLearn);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(new Card("용어", left), 0, 0);
        body.Controls.Add(new Card(null, rtbLearn) { Padding = new Padding(24, 18, 12, 12) }, 1, 0);

        page.Controls.Add(body);
        page.Controls.Add(Header("배우기", "투자 초보를 위한 용어 설명. 다른 화면에서 지표 이름에 마우스를 올리거나 클릭해도 여기로 와요."));
        FillLearnList();
        return page;
    }

    void FillLearnList()
    {
        string q = txtLearn.Text.Trim();
        lstLearn.BeginUpdate();
        lstLearn.Items.Clear();
        foreach (var e in Glossary.All.Where(e => q == "" || e.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                                  || e.Body.Contains(q, StringComparison.OrdinalIgnoreCase)
                                                  || e.Aliases.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase))))
            lstLearn.Items.Add(e);
        lstLearn.EndUpdate();
        if (lstLearn.Items.Count > 0 && lstLearn.SelectedIndex < 0) lstLearn.SelectedIndex = 0;
    }

    void ShowLearn(Glossary.Entry e)
    {
        rtbLearn.Clear();
        void Put(string text, Font f, Color c) { rtbLearn.SelectionFont = f; rtbLearn.SelectionColor = c; rtbLearn.AppendText(text); }
        Put(e.Group + "\n", Theme.Small, Theme.Muted);
        Put(e.Title + "\n", Theme.H1, Theme.Text);
        Put(e.Short + "\n\n", Theme.Bold, Color.FromArgb(96, 165, 250));
        foreach (var raw in e.Body.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("## ")) Put("\n" + line[3..] + "\n", Theme.H2, Theme.Text);
            else Put(line + "\n", Theme.Body, line.StartsWith("•") ? Theme.Text : Color.FromArgb(212, 212, 216));
        }
        rtbLearn.SelectionStart = 0;
        rtbLearn.ScrollToCaret();
    }

    // 다른 화면에서 용어로 바로 이동
    void OpenLearn(string name)
    {
        var e = Glossary.Find(name);
        if (e == null) return;
        txtLearn.Text = "";
        lstLearn.SelectedItem = e;
        ShowPage(PLearn);
    }

    // 표 머리글에 마우스를 올리면 한 줄 설명
    static void AddHeaderTips(DataGridView g)
    {
        foreach (DataGridViewColumn c in g.Columns)
            if (Glossary.Find(c.Name) is Glossary.Entry e) c.ToolTipText = e.Short + "  (클릭하면 자세한 설명)";
    }

    // ================= 투자 계획 =================

    readonly NumericUpDown numStart = new() { Minimum = 0, Maximum = 10_000_000, Value = 1000, Width = 90, ThousandsSeparator = true };
    readonly NumericUpDown numMonthly = new() { Minimum = 0, Maximum = 100_000, Value = 100, Width = 80, ThousandsSeparator = true };
    readonly NumericUpDown numYears = new() { Minimum = 1, Maximum = 40, Value = 10, Width = 55 };
    readonly NumericUpDown numBondYield = new() { Minimum = 0, Maximum = 15, DecimalPlaces = 1, Increment = 0.5m, Value = 3.0m, Width = 60 };
    readonly NumericUpDown numGrowth = new() { Minimum = -10, Maximum = 20, DecimalPlaces = 1, Increment = 0.5m, Value = 4.0m, Width = 60 };
    readonly CheckBox chkReinvest = new() { Text = "배당 재투자", AutoSize = true, Checked = true, Margin = new Padding(12, 8, 0, 0) };
    readonly Button btnMakePlan = Theme.Primary("조합표 만들기");
    readonly Button btnApplyPlan = Theme.Secondary("이 조합으로 포트폴리오 설정");
    readonly DataGridView gridCombo = NewGrid(true);
    readonly DataGridView gridYears = NewGrid(true);
    readonly TermText txtStrategy = new();
    readonly TermText txtMacro = new() { Text = "시장 환경을 받는 중..." };
    bool bondYieldFromMarket;

    async Task RefreshMacro()
    {
        try
        {
            var s = await Macro.Fetch(keys[6]);
            var sig = Macro.Read(s);
            var sb = new StringBuilder();
            foreach (var x in sig)
            {
                sb.AppendLine($"■ {x.Title}:  {x.Value}");
                sb.AppendLine($"   {x.Meaning}");
                sb.AppendLine();
            }
            sb.AppendLine(Macro.Summary(sig));
            sb.Append($"\r\n({s.At:MM.dd HH:mm} 기준 · 국내 금리 한국은행 ECOS, 나머지 Yahoo)");
            txtMacro.SetText(sb.ToString());
        }
        catch (Exception ex)
        {
            txtMacro.SetText("시장 환경을 못 받았어요: " + ex.Message.Split('\n')[0]);
        }
    }

    readonly Label lblPlan = Theme.Text_("목표 비중은 [설정]에서 바꿀 수 있어요. 조합표는 가치 분석 결과와 오늘 시세로 만들어요.", Theme.Small, Theme.Muted);
    List<Plan.Pick> picks = new();

    Control BuildPlanPage()
    {
        var page = Page();
        foreach (var g in new[] { gridCombo, gridYears }) { Theme.StyleGrid(g); g.DataBindingComplete += (s, _) => FormatNumbers((DataGridView)s); }
        foreach (Control c in new Control[] { numStart, numMonthly, numYears, numBondYield, numGrowth, chkReinvest }) Theme.StyleInput(c);
        Theme.DarkScrollbars(txtStrategy);
        Theme.DarkScrollbars(txtMacro);
        btnMakePlan.Click += async (_, _) => await MakePlan();
        btnApplyPlan.Click += (_, _) => ApplyPlan();
        btnApplyPlan.Enabled = false;

        var row1 = Toolbar(Theme.Text_("지금 투자금"), numStart, Theme.Text_("만원    매달"), numMonthly, Theme.Text_("만원    기간"), numYears,
            Theme.Text_("년"), chkReinvest);
        var row2 = Toolbar(Theme.Text_("가정:  채권 이자 연", Theme.Body, Theme.Muted), numBondYield, Theme.Text_("%    주식 가격 상승 연", Theme.Body, Theme.Muted),
            numGrowth, Theme.Text_("%      ", Theme.Body, Theme.Muted), btnMakePlan, btnApplyPlan);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        left.Controls.Add(new Card("추천 조합표", gridCombo), 0, 0);
        left.Controls.Add(new Card("연도별 예상 (세후 배당)", gridYears), 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.Controls.Add(new Card("지금 시장 환경  ·  규칙에 따른 해석, 예측 아님", txtMacro), 0, 0);
        right.Controls.Add(new Card("단계별 전략", txtStrategy), 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);

        var status = new Panel { Dock = DockStyle.Top, Height = 26 };
        lblPlan.Dock = DockStyle.Fill;
        status.Controls.Add(lblPlan);

        page.Controls.Add(body);
        page.Controls.Add(status);
        page.Controls.Add(Theme.Spacer(6));
        page.Controls.Add(row2);
        page.Controls.Add(Theme.Spacer(6));
        page.Controls.Add(row1);
        page.Controls.Add(Header("투자 계획", "목표 비중에 맞춘 추천 조합과, 매달 쌓아 갈 때 몇 년 뒤 얼마의 배당이 들어오는지 보여줘요."));
        return page;
    }

    async Task MakePlan()
    {
        if (allRows.Count == 0) { lblPlan.Text = "시세가 아직 없어요. 잠시 후 다시 눌러주세요."; return; }
        btnMakePlan.Enabled = false;
        // 채권 이자 가정: 처음 한 번은 지금 국고채 3년 금리로 채운다 (그 뒤엔 사용자가 고친 값 유지)
        if (!bondYieldFromMarket && Macro.Last?.Ktb3 is double k3)
        {
            numBondYield.Value = (decimal)Math.Round(k3, 1);
            bondYieldFromMarket = true;
        }
        lblPlan.Text = "ETF 시세와 배당률을 받는 중...";
        try
        {
            var etfSymbols = Plan.Etfs.Values.SelectMany(a => a).Select(e => e.Code + ".KS");
            var q = await Yahoo.Quotes(etfSymbols.Concat(new[] { "VOO", "SCHD" }));
            double? ProxyYield(string p) => p != null && q.TryGetValue(p, out var v) ? v.yield : null;

            picks = new List<Plan.Pick>();
            for (int i = 0; i < 3; i++)
            {
                string cat = Portfolio.Cats[i];
                var cands = Plan.Etfs[cat];
                var e = cands.FirstOrDefault(c => q.TryGetValue(c.Code + ".KS", out var v) && v.price > 0) ?? cands[0];
                double price = q.TryGetValue(e.Code + ".KS", out var pv) ? pv.price : 0;
                double? y = cat == "채권" ? (double)numBondYield.Value : ProxyYield(e.Proxy);
                string alt = string.Join(", ", cands.Where(c => c != e).Select(c => $"{c.Name}({c.Code})"));
                string why = e.Why + (e.Proxy != null ? $" · 배당률은 {e.Proxy} 기준" : " · 이자율은 가정값");
                picks.Add(new Plan.Pick(cat, e.Code, e.Name, Portfolio.Target[i], price, y, why, alt));
            }

            // 위성: 종목당 (위성 비중 ÷ 종목 수), 단 한 종목 한도를 넘지 않게
            var sats = Plan.Satellites(allRows, screenTable, MaybeSpecial, 4);
            double satW = sats.Count == 0 ? 0 : Math.Min(Portfolio.Target[3] / sats.Count, Portfolio.SatelliteMax);
            foreach (var s in sats)
            {
                double? dps = LatestDps(s.Q.Code);
                double? y = dps > 0 && s.Q.Price > 0 ? dps / s.Q.Price * 100 : s.Div;
                picks.Add(new Plan.Pick("위성", s.Q.Code, s.Q.Name, satW, s.Q.Price, y,
                    $"그레이엄 점수 {s.Score} · PER(3년) {s.Per3:0.0} · PBR {s.Pbr:0.00} · 무적자·재무 건전 통과", ""));
            }

            double start = (double)numStart.Value * 1e4, monthly = (double)numMonthly.Value * 1e4;
            var t = new DataTable();
            t.Columns.Add("분류"); t.Columns.Add("종목"); t.Columns.Add("코드");
            foreach (var c in new[] { "비중%", "금액", "현재가", "수량(주)", "배당률%", "연 배당(세후)" }) t.Columns.Add(c, typeof(double));
            t.Columns.Add("선정 이유"); t.Columns.Add("대안");
            foreach (var p in picks)
            {
                double amt = start * p.Weight;
                t.Rows.Add(p.Cat, p.Name, p.Code, p.Weight * 100, amt, p.Price, p.Price > 0 ? Math.Floor(amt / p.Price) : 0,
                    p.Yield.HasValue ? p.Yield.Value : DBNull.Value, amt * (p.Yield ?? 0) / 100 * (1 - Income.TaxRate), p.Why, p.Alternatives);
            }
            if (sats.Count == 0) t.Rows.Add("위성", "후보 없음", "", Portfolio.Target[3] * 100, start * Portfolio.Target[3], 0, 0, DBNull.Value, 0,
                "[가치 분석]을 먼저 실행하세요", "");
            gridCombo.DataSource = t;

            // 분류별 배당률 (위성은 고른 종목 평균) → 연도별 예상
            double CatYield(string cat) => picks.Where(p => p.Cat == cat).Select(p => p.Yield ?? 0).DefaultIfEmpty(0).Average() / 100;
            var yields = Portfolio.Cats.Select(CatYield).ToArray();
            int years = (int)numYears.Value;
            var proj = Plan.Project(start, monthly, years, Portfolio.Target, yields, (double)numGrowth.Value / 100, chkReinvest.Checked);
            var yt = new DataTable();
            yt.Columns.Add("연차", typeof(int));
            foreach (var c in new[] { "누적 원금", "예상 자산", "그 해 배당", "월 평균 배당" }) yt.Columns.Add(c, typeof(double));
            foreach (var y in proj) yt.Rows.Add(y.N, y.Principal, y.Assets, y.Dividend, y.MonthlyDividend);
            gridYears.DataSource = yt;

            txtStrategy.SetText(StrategyText(start, monthly, years, yields, proj, sats.Count, satW));
            btnApplyPlan.Enabled = true;
            lblPlan.Text = $"{DateTime.Now:HH:mm} 기준 · 환율·시세는 Yahoo, 위성은 가치 분석({(File.Exists(Market.ScreenFile) ? File.GetLastWriteTime(Market.ScreenFile).ToString("MM.dd") : "없음")}) 결과 · 추정치이고 투자 권유가 아니에요.";
        }
        catch (Exception ex)
        {
            lblPlan.Text = "조합표를 못 만들었어요: " + ex.Message.Split('\n')[0];
        }
        finally
        {
            btnMakePlan.Enabled = true;
        }
    }

    string StrategyText(double start, double monthly, int years, double[] yields, List<Plan.Year> proj, int satCount, double satW)
    {
        double goal = (double)numGoal.Value * 1e4;
        string Name(string cat) => string.Join(", ", picks.Where(p => p.Cat == cat).Select(p => p.Name));
        var y3 = proj[Math.Min(2, proj.Count - 1)];
        var last = proj[^1];
        var hit = proj.FirstOrDefault(y => y.MonthlyDividend >= goal);

        // 기간 안에 목표에 못 미치면, 필요한 월 적립액을 찾는다 (이분 탐색)
        string need = "";
        if (hit == null)
        {
            double lo = monthly, hi = Math.Max(monthly, 1e4) * 64;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                var p = Plan.Project(start, mid, years, Portfolio.Target, yields, (double)numGrowth.Value / 100, chkReinvest.Checked);
                if (p[^1].MonthlyDividend >= goal) hi = mid; else lo = mid;
            }
            need = $"   → {years}년 안에 받으려면 매달 약 {Krw(Math.Ceiling(hi / 1e4) * 1e4)}을 넣어야 해요.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("【1단계 · 시작 — 이번 달 ~ 3개월】");
        sb.AppendLine($"• 지금 투자금 {Krw(start)}을 조합표대로 사요. 한 번에 사기 부담되면 3개월에 나눠 사요(분할 매수).");
        sb.AppendLine($"• 채권 {Portfolio.Target[0] * 100:0}% {Name("채권")} / 지수 {Portfolio.Target[1] * 100:0}% {Name("지수")} / 배당 {Portfolio.Target[2] * 100:0}% {Name("배당")}");
        sb.AppendLine(satCount > 0
            ? $"• 위성 {Portfolio.Target[3] * 100:0}%: {Name("위성")} — 종목당 {satW * 100:0.#}% (한 종목 ≤ {Portfolio.SatelliteMax * 100:0.#}%)"
            : "• 위성: [가치 분석]을 먼저 실행하면 후보가 채워져요. 그 전엔 위성 몫을 배당 ETF에 둬도 돼요.");
        sb.AppendLine("• [이 조합으로 포트폴리오 설정]을 누르면 매달 살 ETF가 정해져요.");
        sb.AppendLine();
        sb.AppendLine("【2단계 · 쌓기 — 1 ~ 3년차】");
        sb.AppendLine($"• 매달 {Krw(monthly)}을 넣고 [포트폴리오] 화면의 [잔고 조회 · 배분 계산]대로 사요.");
        sb.AppendLine($"• 비중이 ±{Portfolio.Band * 100:0.#}%p 넘게 벗어나면 그달 적립금을 부족한 쪽에 몰아 맞춰요. 파는 대신 사서 맞추기.");
        sb.AppendLine(chkReinvest.Checked ? "• 배당은 쓰지 않고 다시 사는 데 써요(재투자)." : "• 배당은 받아서 써요(재투자 안 함).");
        sb.AppendLine("• 분기마다 [가치 분석]을 다시 돌려 위성 종목 점검: 점수 5 미만, 적자 전환, PER(3년) 15 초과, 특별배당 경고가 뜨면 다음 적립부터 교체 후보.");
        sb.AppendLine($"• 3년차 예상: 자산 {Krw(y3.Assets)}, 월 배당 {Krw(y3.MonthlyDividend)}");
        sb.AppendLine();
        sb.AppendLine($"【3단계 · 굴리기 — 4 ~ {years}년차】");
        sb.AppendLine("• 1년에 한 번(예: 1월) 비중 점검. 위성은 3 ~ 5종목 유지.");
        sb.AppendLine($"• {years}년차 예상: 원금 {Krw(last.Principal)} → 자산 {Krw(last.Assets)}, 월 배당 {Krw(last.MonthlyDividend)} (세후)");
        sb.AppendLine(hit != null
            ? $"• 목표 월 배당 {Krw(goal)}: {hit.N}년차에 도달할 것으로 보여요."
            : $"• 목표 월 배당 {Krw(goal)}: {years}년 안에는 못 미쳐요.{need}");
        sb.AppendLine();
        sb.AppendLine("【4단계 · 받기 — 목표에 닿은 뒤】");
        sb.AppendLine("• 배당 재투자를 멈추고 생활비로 받아요. 주식은 팔지 않고 배당만 꺼내 쓰는 게 원칙.");
        sb.AppendLine("• 쓸 때가 가까워질수록 채권 비중을 늘리는 것도 검토해요 ([설정] > 포트폴리오 목표).");
        sb.AppendLine();
        sb.AppendLine("【가정】");
        sb.AppendLine($"채권 이자 {yields[0] * 100:0.0}%, 주식 가격 상승 연 {numGrowth.Value:0.0}%, " +
                      $"지수 배당 {yields[1] * 100:0.00}%, 배당 ETF {yields[2] * 100:0.00}%, 위성 평균 {yields[3] * 100:0.00}%, 배당소득세 15.4%. " +
                      "목표 월 배당은 [배당 수입] 화면의 값을 써요. 실제 수익·배당은 달라질 수 있는 추정치이고 투자 권유가 아니에요.");
        return sb.ToString();
    }

    // 고른 ETF를 classes.txt에 '매수' 대상으로 적는다. 다른 줄과 주석은 그대로 둔다.
    void ApplyPlan()
    {
        var lines = File.Exists(Portfolio.ClassFile) ? File.ReadAllLines(Portfolio.ClassFile).ToList() : new List<string>();
        var codes = picks.Select(p => p.Code).ToHashSet();
        var etfCats = picks.Where(p => p.Cat != "위성").Select(p => p.Cat).ToHashSet();
        lines = lines.Where(l =>
        {
            var p = l.Split(',').Select(s => s.Trim()).ToArray();
            if (p.Length < 2 || l.TrimStart().StartsWith("#")) return true;
            if (codes.Contains(p[0])) return false;                                  // 새로 쓸 줄
            return !(p.Length > 2 && p[2] == "매수" && etfCats.Contains(p[1]));       // 같은 분류의 예전 '매수' 지정은 뺀다
        }).ToList();
        lines.Add($"# 투자 계획 {DateTime.Now:yyyy-MM-dd}");
        foreach (var p in picks) lines.Add(p.Cat == "위성" ? $"{p.Code},위성" : $"{p.Code},{p.Cat},매수");
        Directory.CreateDirectory(Market.Dir);
        File.WriteAllLines(Portfolio.ClassFile, lines);
        lblPlan.Text = $"포트폴리오 설정 완료: {string.Join(", ", picks.Where(p => p.Cat != "위성").Select(p => p.Name))}을 매달 적립 대상으로 정했어요.";
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
        usQuotes = Market.LoadUsCached();
        krTrends = Market.LoadKrTrends();
        if (quotes != null || usQuotes != null) RebuildRows();
    }

    Dictionary<string, Yahoo.Trend> krTrends;
    List<SectorStat> sectorStats = new();
    bool fetchingDivs;

    // 종합 점수: 배당률 → 배당 기록 → 업종 통계 → 점수 → 라벨
    void ScoreRows()
    {
        foreach (var r in allRows)
        {
            double? dps = LatestDps(r.Q.Code);
            r.Yield = r.Q.IsUs ? r.Q.DivYield : dps > 0 && r.Q.Price > 0 ? dps / r.Q.Price * 100 : null;
            r.DivI = Dividends.Get(Yahoo.Symbol(r.Q));
        }
        sectorStats = Score.Sectors(allRows);
        var bySector = sectorStats.ToDictionary(s => (s.Region, s.Sector));
        foreach (var r in allRows)
        {
            r.Sec = r.Q.Sector != null ? bySector.GetValueOrDefault((Score.Region(r.Q), r.Q.Sector)) : null;
            Score.Compute(r, r.Sec, r.DivI);
            Labels.Enrich(r);
        }
    }

    // 배당 기록(주기·안정성)은 배당을 주는 종목만 백그라운드로 받는다. 1주일 지난 것만 다시.
    async Task FetchDividendHistories()
    {
        if (fetchingDivs || allRows.Count == 0) return;
        fetchingDivs = true;
        try
        {
            var symbols = allRows.Where(r => r.Q.IsUs ? r.Yield >= 1 : r.Yield > 0) // 미국은 배당률 1% 이상만
                                 .OrderByDescending(r => r.Q.Cap).Select(r => Yahoo.Symbol(r.Q))
                                 .Concat(Plan.Etfs.Values.SelectMany(a => a).Select(e => e.Code + ".KS"))
                                 .ToList();
            await Dividends.FetchMany(symbols, new Progress<string>(s => refreshNote = "  ·  " + s));
            RebuildRows();
        }
        finally { fetchingDivs = false; }
    }

    List<Quote> usQuotes;
    DateTime lastRefresh;
    string refreshNote = "";
    readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 30 * 60 * 1000 };
    bool refreshing;

    // 국장(공공데이터, 하루 1번 종가)과 미장(Yahoo, 약 15분 지연)을 각각 받는다. 한쪽이 실패해도 다른 쪽은 갱신.
    async Task RefreshQuotes()
    {
        if (refreshing) return;
        refreshing = true;
        var fails = new List<string>();
        try
        {
            if (quotes == null && usQuotes == null) lblMarket.Text = "전 종목 시세를 받는 중...";
            if (!string.IsNullOrWhiteSpace(keys[0]))
            {
                try { (basDt, quotes) = await Market.Fetch(keys[0], Screener.NewHttp()); }
                catch (Exception ex) { fails.Add("국장: " + ex.Message.Split('\n')[0]); }
            }
            else fails.Add("국장: 설정에서 공공데이터포털 키를 넣어주세요");
            try { usQuotes = await Market.FetchUs(); }
            catch (Exception ex) { fails.Add("미장: " + ex.Message.Split('\n')[0]); }
            try { krTrends = await Market.FetchKrTrends(); }
            catch (Exception ex) { fails.Add("국장 추세: " + ex.Message.Split('\n')[0]); }

            lastRefresh = DateTime.Now;
            refreshNote = fails.Count == 0 ? "" : "  ·  갱신 실패 — " + string.Join(", ", fails);
            if (quotes != null || usQuotes != null) RebuildRows();
            else lblMarket.Text = "시세를 못 받았어요" + refreshNote;
        }
        finally
        {
            refreshing = false;
        }
    }

    // 30분마다: 시세 갱신 + 가치 분석 결과가 하루 넘게 지났으면 다시 분석
    async Task OnRefreshTimer()
    {
        await RefreshMacro(); // 지수 1년 등락을 상세 패널 비교에도 쓴다
        await RefreshQuotes();
        _ = FetchDividendHistories(); // 기다리지 않는다 (처음엔 몇 분 걸림)
        bool stale =!File.Exists(Market.ScreenFile) || File.GetLastWriteTime(Market.ScreenFile) < DateTime.Now.AddDays(-1);
        if (stale && btnRun.Enabled && !string.IsNullOrWhiteSpace(keys[0]) && !string.IsNullOrWhiteSpace(keys[1]))
            await RunScreen(auto: true);
    }

    void RebuildRows()
    {
        allRows = Labels.Build((Market.WithTrends(quotes, krTrends) ?? new()).Concat(usQuotes ?? new()).ToList(), screenTable);
        rowByCode = allRows.ToDictionary(r => r.Q.Code);
        ScoreRows();
        ApplySearch();
        RefreshSectors();

        // 배당 목록 현재가를 최신 시세로 (시세 API에 없는 ETF는 그대로)
        foreach (var i in incomeItems)
            if (rowByCode.TryGetValue(i.Code, out var r)) i.Price = r.Q.Price;
        RefreshIncome();
    }

    void ApplySearch()
    {
        if (quotes == null && usQuotes == null) return;
        var source = marketFilter == null ? allRows
                   : marketFilter == "US" ? allRows.Where(r => r.Q.IsUs)
                   : allRows.Where(r => r.Q.Market == marketFilter);
        var found = Labels.Search(source, txtSearch.Text, out var rule);

        var t = new DataTable();
        t.Columns.Add("종목코드"); t.Columns.Add("시장"); t.Columns.Add("종목명"); t.Columns.Add("업종");
        foreach (var c in new[] { "시가총액(억)", "현재가", "등락%", "배당%" }) t.Columns.Add(c, typeof(double));
        t.Columns.Add("배당주기");
        foreach (var c in new[] { "고점대비%", "1년%", "종합점수" }) t.Columns.Add(c, typeof(double));
        object N(double? v) => v.HasValue ? v.Value : DBNull.Value;
        foreach (var r in found)
            t.Rows.Add(r.Q.Code, MarketName(r.Q.Market), r.Q.Name, Yahoo.SectorKo(r.Q.Sector) ?? (r.Q.Market == "US ETF" ? "ETF" : ""),
                Math.Round(r.Q.Cap / 1e8), r.Q.Price, r.Q.ChangeRate, N(r.Yield ?? r.Div),
                r.DivI == null ? "" : r.DivI.Freq == "없음" ? "-" : r.DivI.Freq + (r.DivI.Special ? "·특별" : ""),
                N(r.Q.FromHigh), N(r.Q.Chg1y), N(r.Total));
        gridMarket.DataSource = t;
        FitColumns(gridMarket, t, "종목명");

        string date = basDt?.Length == 8 ? $"{basDt[..4]}.{basDt[4..6]}.{basDt[6..]}" : basDt;
        string sort = rule == null ? "시총 큰 순" : $"'{rule.Label}' · {SortName(rule)}";
        string hint = rule?.NeedsScreen == true && screenTable == null ? "   ※ 배당·가치 검색은 [가치 분석]을 한 번 실행하면 쓸 수 있어요" : "";
        string refreshed = lastRefresh == default ? "" : $"  ·  {lastRefresh:HH:mm} 갱신 (30분마다)";
        lblMarket.Text = $"국장 {date} 종가 · 미장 15분 지연 · 환율 {Market.UsdKrw:N1}원  ·  {allRows.Count:N0}종목 중 {found.Count:N0}개  ·  정렬: {sort}{refreshed}{hint}{refreshNote}";
    }

    // 숫자가 잘리지 않게 열마다 (머리글, 가장 긴 값) 중 넓은 쪽으로 맞추고, fillColumn이 남는 폭을 채운다
    static readonly Dictionary<string, string> ColFormat = new()
    {
        ["현재가"] = "N0", ["시가총액(억)"] = "N0", ["등락%"] = "+0.00;-0.00;0.00", ["배당%"] = "0.00",
        ["고점대비%"] = "0.0", ["1년%"] = "+0.0;-0.0;0.0", ["종합점수"] = "0",
        ["평가금액(원)"] = "N0", ["주당배당금"] = "N0", ["배당률%"] = "0.00", ["연 배당(원)"] = "N0", ["월 배당(원)"] = "N0", ["수량"] = "N0",
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
        "저PER" => "PER(3년 평균) 낮은 순",
        "저PBR" => "PBR 낮은 순",
        "고ROE" or "무적자" => "ROE 높은 순",
        "성장주" => "이익성장 높은 순",
        "재무건전" => "부채비율 낮은 순",
        "그레이엄 우수" => "점수 높은 순",
        "급등" => "등락률 높은 순",
        "급락" => "등락률 낮은 순",
        "거래대금 상위" => "거래대금 큰 순",
        "종합점수" or "월배당" or "분기배당" or "배당안정" => "종합 점수 높은 순",
        "낙폭과대" => "고점 대비 많이 내린 순",
        "고점근처" => "고점에 가까운 순",
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
            case "현재가":
                e.CellStyle.Format = (gridMarket.Rows[e.RowIndex].Cells["시장"].Value as string ?? "").StartsWith("미국") ? "$#,##0.00" : "N0";
                break;
            case "시가총액(억)":
                e.CellStyle.Format = "N0";
                break;
            case "배당%" when e.Value is double d:
                e.CellStyle.Format = "0.00";
                if (d >= 3) e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Color.FromArgb(52, 211, 153); // 고배당 강조
                break;
            case "고점대비%" when e.Value is double h:
                e.CellStyle.Format = "0.0";
                if (h <= -30) e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Color.FromArgb(251, 146, 60); // 많이 내린 종목
                break;
            case "1년%" when e.Value is double y1:
                e.CellStyle.Format = "+0.0;-0.0;0.0";
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = y1 > 0 ? Theme.Up : y1 < 0 ? Theme.Down : Theme.Muted;
                break;
            case "종합점수" when e.Value is double sc:
                e.CellStyle.Format = "0";
                e.CellStyle.Font = Theme.Bold;
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor =
                    sc >= 70 ? Color.FromArgb(52, 211, 153) : sc >= 50 ? Color.FromArgb(251, 191, 36) : Theme.Muted;
                break;
            case "종목명":
                e.CellStyle.Font = Theme.Bold;
                break;
            case "종목코드" or "시장" or "업종" or "배당주기":
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Theme.Muted;
                break;
        }
    }

    // 1년 등락을 업종 중앙값·시장 지수와 비교, 이익 성장도 같은 업종(가치 분석 종목)과 비교
    void FillCompare(StockRow r)
    {
        var q = r.Q;
        if (q.Chg1y == null) { dCompare.SetText("추세 자료가 아직 없어요 (Yahoo 갱신 후 보여요).", Theme.Muted); return; }
        var idx = Macro.Last?.Get(q.IsUs ? "S&P500" : q.Market == "KOSDAQ" ? "코스닥" : "코스피");
        var sb = new StringBuilder();
        sb.Append($"1년: 이 종목 {q.Chg1y:+0.0;-0.0}%");
        if (r.Sec != null) sb.Append($" · {Yahoo.SectorKo(r.Sec.Sector)} 업종 {r.Sec.Chg1y:+0.0;-0.0}%");
        if (idx != null) sb.Append($" · {idx.Name} {idx.Chg1y:+0.0;-0.0}%");
        sb.AppendLine();
        var verdict = new List<string>();
        if (r.Sec != null) verdict.Add($"업종보다 {Math.Abs(q.Chg1y.Value - r.Sec.Chg1y):0.0}%p {(q.Chg1y >= r.Sec.Chg1y ? "강해요" : "약해요")}");
        if (idx != null) verdict.Add($"시장보다 {Math.Abs(q.Chg1y.Value - idx.Chg1y):0.0}%p {(q.Chg1y >= idx.Chg1y ? "강해요" : "약해요")}");
        if (verdict.Count > 0) sb.AppendLine("→ " + string.Join(", ", verdict));
        sb.AppendLine($"고점 대비 {q.FromHigh:0.0}% · 50일선 {q.Vs50:+0.0;-0.0}% · 200일선 {q.Vs200:+0.0;-0.0}%");
        if (r.Sec != null) sb.Append($"업종 국면: {r.Sec.Phase} (단기 {r.Sec.Vs50:+0.0;-0.0}%, 중장기 {r.Sec.Vs200:+0.0;-0.0}%)");
        if (r.Growth != null && r.Q.Sector != null)
        {
            var peers = allRows.Where(x => !x.Q.IsUs && x.Q.Sector == r.Q.Sector && x.Growth != null).Select(x => x.Growth.Value).OrderBy(x => x).ToList();
            if (peers.Count >= 3) sb.Append($"\r\n이익 성장 {r.Growth:+0;-0}% · 같은 업종 분석 종목 중앙값 {peers[peers.Count / 2]:+0;-0}%");
        }
        dCompare.SetText(sb.ToString());
    }

    void FillDivInfo(StockRow r)
    {
        var d = r.DivI;
        string money(double v) => r.Q.IsUs ? $"${v:N2}" : $"{v:N0}원";
        string head = d == null ? "배당 기록을 받는 중이에요..."
            : d.Freq == "없음" ? "최근 10년 배당 기록이 없어요."
            : $"{d.Freq}배당 (연 {d.PerYear}회) · 최근 1년 정기배당 {money(d.Ttm)} · 연속 {d.Years}년 · 최근 5년 삭감 {d.Cuts}번" +
              (d.Growth3y is double g ? $" · 3년 배당 성장 연 {g:+0.0;-0.0}%" : "") + (d.Special ? " · ⚠ 특별배당 있음" : "");
        string score = r.Total is double t ? $"\r\n종합 점수 {t:0}점  ({r.ScoreNote})" : "\r\n종합 점수: 자료가 부족해요";
        dDivInfo.SetText(head + score, Labels.IsStable(r) ? Color.FromArgb(52, 211, 153) : null);
    }

    static string MarketName(string m) => m switch
    {
        "KOSPI" => "코스피", "KOSDAQ" => "코스닥", "US" => "미국", "US ETF" => "미국 ETF", _ => m,
    };

    void ShowDetail()
    {
        if (gridMarket.CurrentRow?.Cells["종목코드"].Value is not string code || !rowByCode.TryGetValue(code, out var r)) return;
        var q = r.Q;
        dName.Text = q.Name;
        dSub.Text = $"{q.Code}  ·  {MarketName(q.Market)}  ·  시총 {r.Rank:0}위";
        string sign = q.Change > 0 ? "▲" : q.Change < 0 ? "▼" : "";
        dPrice.Text = q.IsUs
            ? $"${q.Price:N2}   {sign}{Math.Abs(q.Change):N2} ({q.ChangeRate:+0.00;-0.00;0.00}%)"
            : $"{q.Price:N0}원   {sign}{Math.Abs(q.Change):N0} ({q.ChangeRate:+0.00;-0.00;0.00}%)";
        dPrice.ForeColor = q.Change > 0 ? Theme.Up : q.Change < 0 ? Theme.Down : Theme.Text;

        double? dps = LatestDps(code);
        if (dps > 0 && q.Price > 0)
        {
            double yield = dps.Value / q.Price, tax = Income.Tax(q.Currency);
            double perTenM = 10_000_000 * yield; // 1천만원어치를 산다고 가정 (환율과 무관)
            dDivCalc.SetText($"1,000만원 투자 시  연 {perTenM:N0}원 · 월 {perTenM / 12:N0}원\r\n" +
                             $"세후({tax * 100:0.#}%)  연 {perTenM * (1 - tax):N0}원 · 월 {perTenM * (1 - tax) / 12:N0}원\r\n" +
                             (q.IsUs ? $"주당 연 ${dps:N2} · 배당률 {yield * 100:0.00}% (Yahoo)" : $"주당 {dps:N0}원 · 현재가 기준 배당률 {yield * 100:0.00}%") +
                             (MaybeSpecial(code) ? "\r\n⚠ 지난 결산 배당이 평소의 2배 이상 — 일회성 특별배당일 수 있어요" : ""),
                             MaybeSpecial(code) ? Color.FromArgb(251, 191, 36) : null);
        }
        else
        {
            dDivCalc.SetText(q.IsUs ? "배당을 주지 않거나 배당 정보가 없어요."
                             : dps == 0 ? "지난 결산에 현금배당이 없었어요." : "배당 정보가 아직 없어요. 담으면 DART에서 찾아봐요.", Theme.Muted);
        }

        string F(double? v, string fmt, string unit = "") => v.HasValue ? v.Value.ToString(fmt) + unit : "—";
        metric["시총"].Text = $"{q.Cap / 1e12:0.0}조";
        metric["거래대금"].Text = $"{q.TradeValue / 1e8:N0}억";
        metric["PER(3년)"].Text = F(r.Per3, "0.0");
        metric["PER(3년)"].ForeColor = r.Per3 <= 15 ? Theme.Good : r.Per3 != null ? Theme.Bad : Theme.Text; // 그레이엄 기준 15배
        metric["PER(1년)"].Text = F(r.Per, "0.0");
        metric["PBR"].Text = F(r.Pbr, "0.00");
        metric["ROE"].Text = F(r.Roe, "0.0", "%");
        metric["배당수익률"].Text = F(r.Div, "0.0", "%");
        metric["부채비율"].Text = F(r.DebtRatio, "0", "%");
        metric["이익성장"].Text = F(r.Growth, "0", "%");
        metric["점수"].Text = r.Score?.ToString() ?? "—";
        metric["배당 연수"].Text = r.DivYears?.ToString() ?? "—";

        FillCompare(r);
        FillDivInfo(r);

        // 시세 그래프·회사 소개는 잠깐 멈췄을 때 받는다
        detailCode = code;
        detailVersion++;
        detailDelay.Stop();
        detailDelay.Start();

        var s = series.GetValueOrDefault(code);
        niChart.SetData(s?.NetIncome);
        dpsChart.SetData(s?.Dps?.Where(kv => kv.Key >= 2015).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    // ================= 스크리닝 =================

    // auto = 30분 타이머가 하루 지난 결과를 다시 분석할 때 (오류 창을 띄우지 않는다)
    async Task RunScreen(bool auto = false)
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
            RebuildRows();
        }
        catch (Exception ex)
        {
            lblStatus.Text = auto ? "자동 분석 실패: " + ex.Message.Split('\n')[0] : "오류가 났어요. 메시지를 Claude에게 보내주세요.";
            if (!auto) MessageBox.Show(ex.Message, "오류");
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
