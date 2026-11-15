using System.Data;
using System.Text;

namespace KrxScreen;

// 시뮬레이터 › 게임 모드 화면. 엔진은 Game.cs. 실제 계좌(키움)와는 연결하지 않는다.
public partial class MainForm
{
    readonly NumericUpDown numGameSeed = new() { Minimum = 0, Maximum = 10_000_000, Increment = 100, Value = 0, Width = 110, ThousandsSeparator = true };
    readonly NumericUpDown numGameMonthly = new() { Minimum = 0, Maximum = 100_000, Increment = 10, Value = 0, Width = 90, ThousandsSeparator = true };
    readonly NumericUpDown numGameQty = new() { Minimum = 1, Maximum = 10_000_000, Value = 1, Width = 90, ThousandsSeparator = true };
    readonly Label lblGameClock = Theme.Text_("", Theme.Bold);
    readonly Label lblGameMsg = Theme.Text_("", Theme.Small, Theme.Muted);
    readonly Label lblGamePick = Theme.Text_("종목을 찾아 고르세요", Theme.Bold, Theme.Muted);
    readonly KpiTile kGameTotal = new("총자산"), kGameCash = new("현금"), kGameGain = new("손익 (넣은 돈 대비)"), kGameDiv = new("받은 배당 (세후)"), kGameSeason = new("이번 시즌");
    readonly DataGridView gridGame = NewGrid(true);
    readonly TermText txtGameNews = new() { Dock = DockStyle.Fill };
    readonly Panel gameStartPanel = new() { Dock = DockStyle.Top, Height = 470 }, gameMainPanel = new() { Dock = DockStyle.Fill };
    readonly NumericUpDown numSalary = new() { Minimum = 0, Maximum = 10_000_000, Increment = 100, Width = 100, ThousandsSeparator = true };
    readonly NumericUpDown numLiving = new() { Minimum = 0, Maximum = 100_000, Increment = 10, Width = 80, ThousandsSeparator = true };
    readonly NumericUpDown numRaise = new() { Minimum = 0, Maximum = 30, DecimalPlaces = 1, Increment = 0.5m, Value = 3, Width = 60 };
    readonly CheckBox chkSalaryPack = new() { AutoSize = true }, chkBankPack = new() { AutoSize = true };
    readonly ComboBox cmbItemKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    readonly TextBox txtItemName = new() { Width = 130, PlaceholderText = "이름 (예: 전세대출)" };
    readonly NumericUpDown numItemAmt = new() { Minimum = 0, Maximum = 10_000_000, Increment = 100, Width = 100, ThousandsSeparator = true };
    readonly NumericUpDown numItemRate = new() { Minimum = 0, Maximum = 30, DecimalPlaces = 2, Increment = 0.1m, Value = 4, Width = 70 };
    readonly NumericUpDown numItemPay = new() { Minimum = 0, Maximum = 1_000_000, Increment = 10, Width = 80, ThousandsSeparator = true };
    readonly NumericUpDown numItemMonths = new() { Minimum = 1, Maximum = 600, Value = 12, Width = 60 };
    readonly ListBox lstGameItems = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    readonly Label lblGameFlow = Theme.Text_("", Theme.Bold), lblSetupNote = Theme.Text_("", Theme.Small, Theme.Muted);
    readonly Label lblAmtUnit = Theme.Text_("만원 잔액", Theme.Body), lblPayUnit = Theme.Text_("만원 매달 갚기", Theme.Body);
    readonly Button btnGameStart = Theme.Primary("게임 시작"), btnSetupClose = Theme.Secondary("닫기");
    readonly List<FinItem> setupItems = new();
    readonly System.Windows.Forms.Timer gameClock = new() { Interval = 1000 };
    readonly Dictionary<string, double> gamePrices = new();                 // 종목코드 → 지금 가격 (그 나라 통화)
    readonly Dictionary<string, List<Yahoo.Point>> gameDivs = new();       // 종목코드 → 배당 기록
    StockRow gamePick;
    bool gameTicking, gameWelcomed, gameSessionTicked;
    DateTime gameLastTick;

    Control BuildGamePage()
    {
        var page = Page();
        foreach (Control c in new Control[] { numGameSeed, numGameMonthly, numGameQty }) Theme.StyleInput(c);

        // ---- 재무 설정 · 확장팩 (새 게임 시작, 게임 중 바꾸기) ----
        gameStartPanel.Controls.Add(new Card("내 정보 · 초기 설정 (연봉·생활비 · 대출·적금·예금 확장팩)", BuildGameSetup()) { Dock = DockStyle.Fill, Margin = Padding.Empty });

        // ---- 게임 화면 ----
        Theme.DarkScrollbars(txtGameNews);
        txtGameNews.OpenFull = e => OpenLearn(e.Key);
        var picker = new StockPicker(SearchRows, "살 종목 · 팔 종목 찾기") { Width = (int)(300 * DeviceDpi / 96f) };
        picker.Picked += r => { gamePick = r; lblGamePick.ForeColor = Theme.Text; lblGamePick.Text = $"{r.Q.Name} · {Won.Price(r.Q.Price, r.Q.Currency)}"; };
        var btnBuy = Theme.Primary("매수");
        var btnSell = Theme.Secondary("매도");
        btnBuy.Click += async (_, _) => await GameOrder(true);
        btnSell.Click += async (_, _) => await GameOrder(false);
        var btnSetup = Theme.Secondary("내 정보");
        btnSetup.Click += (_, _) => { if (gameStartPanel.Visible) gameStartPanel.Visible = false; else OpenGameSetup(); };
        var btnReset = Theme.Secondary("새 게임");
        btnReset.Click += (_, _) =>
        {
            if (MessageBox.Show("지금 게임을 끝내고 새로 시작할까요? 가상 계좌 기록이 지워져요.", "새 게임", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Game.S.Started = false;
            Game.Save();
            ShowGameState();
        };
        gridGame.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && gridGame.Rows[e.RowIndex].Cells["코드"].Value is string code)
            {
                gamePick = RowFor(code, gridGame.Rows[e.RowIndex].Cells["종목"].Value as string);
                lblGamePick.ForeColor = Theme.Text;
                lblGamePick.Text = $"{gamePick.Q.Name} · 가진 {Game.S.Holdings.FirstOrDefault(h => h.Code == code)?.Qty ?? 0:N0}주";
            }
        };
        gridGame.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.Value is not double v) return;
            string n = gridGame.Columns[e.ColumnIndex].Name;
            e.CellStyle.Format = n == "손익%" ? "+0.0;-0.0;0.0" : "N0";
            if (n is "손익%" or "손익(원)") e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = v > 0 ? Theme.Up : v < 0 ? Theme.Down : Theme.Muted;
        };

        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, ColumnCount = 5, Margin = Padding.Empty };
        for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        kpis.Controls.AddRange(new Control[] { kGameTotal, kGameGain, kGameCash, kGameDiv, kGameSeason });

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        body.Controls.Add(new Card("가상 잔고 · 두 번 누르면 그 종목을 골라요", gridGame), 0, 0);
        body.Controls.Add(new Card("소식 · 매매 · 배당 · 월급", txtGameNews), 1, 0);

        var clockRow = new Panel { Dock = DockStyle.Top, Height = 28 };
        lblGameClock.Dock = DockStyle.Fill;
        clockRow.Controls.Add(lblGameClock);
        var msgRow = new Panel { Dock = DockStyle.Top, Height = 24 };
        lblGameMsg.Dock = DockStyle.Fill;
        msgRow.Controls.Add(lblGameMsg);

        gameMainPanel.Controls.Add(body);
        gameMainPanel.Controls.Add(Theme.Spacer(12));
        gameMainPanel.Controls.Add(kpis);
        gameMainPanel.Controls.Add(msgRow);
        gameMainPanel.Controls.Add(Toolbar(picker, lblGamePick, Theme.Text_("   수량"), numGameQty, Theme.Text_("주  "), btnBuy, btnSell, Theme.Text_("      "), btnSetup, btnReset));
        gameMainPanel.Controls.Add(clockRow);

        page.Controls.Add(gameMainPanel);
        page.Controls.Add(gameStartPanel);
        page.Controls.Add(Header("시뮬레이터 · 게임 모드", "실제 시세 · 실제 시간으로 굴리는 가상 투자. 시드머니는 내가 정하고, 실제 주문은 나가지 않아요."));

        // 시계는 1초마다, 시간 흐름 처리는 5분마다 (화면을 보고 있을 때는 1분마다)
        gameClock.Tick += async (_, _) =>
        {
            UpdateGameClock();
            var every = pages[PGame].page.Visible ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
            if (Game.S.Started && DateTime.Now - gameLastTick >= every) await GameTick(false);
        };
        gameClock.Start();
        pages_VisibleHook = () => { if (!gameWelcomed && Game.S.Started) { gameWelcomed = true; _ = GameTick(true); } };
        ShowGameState();
        return page;
    }

    Action pages_VisibleHook;

    // 내 정보 (첫 시작 전 1회 초기 설정, 이후 [내 정보]로 언제든 바꾸기)
    // 시드머니(새 게임만), 매달 투자금, 확장팩(연봉·생활비 / 대출·적금·예금)
    Control BuildGameSetup()
    {
        foreach (Control c in new Control[] { numSalary, numLiving, numRaise, cmbItemKind, txtItemName, numItemAmt, numItemRate, numItemPay, numItemMonths }) Theme.StyleInput(c);
        lstGameItems.BackColor = Theme.InputBg; lstGameItems.ForeColor = Theme.Text; lstGameItems.Font = Theme.Body;
        var packs = GamePacks.All.ToDictionary(p => p.Key);
        chkSalaryPack.Text = $"확장팩 · {packs["salary"].Name}"; chkBankPack.Text = $"확장팩 · {packs["bank"].Name}";
        foreach (var c in new[] { chkSalaryPack, chkBankPack }) { c.Font = Theme.Bold; c.ForeColor = Theme.Text; }
        cmbItemKind.Items.AddRange(new object[] { "대출", "적금", "예금" });
        cmbItemKind.SelectedIndex = 0;

        Control Row(params Control[] cs) { var r = Toolbar(cs); r.BackColor = Theme.CardBg; r.Padding = new Padding(0, 2, 0, 4); return r; }
        Label Note(string t) { var l = Theme.Text_(t, Theme.Small, Theme.Muted); l.Dock = DockStyle.Top; l.Height = 22; return l; }

        void Sync()
        {
            numGameMonthly.Enabled = !chkSalaryPack.Checked;
            foreach (Control c in new Control[] { numSalary, numLiving, numRaise }) c.Enabled = chkSalaryPack.Checked;
            foreach (Control c in new Control[] { cmbItemKind, txtItemName, numItemAmt, numItemRate, numItemPay, numItemMonths, lstGameItems }) c.Enabled = chkBankPack.Checked;
            string kind = cmbItemKind.SelectedItem as string;
            lblAmtUnit.Text = kind switch { "대출" => "만원 남은 빚", "적금" => "만원 매달 넣기", _ => "만원 맡긴 돈" };
            numItemPay.Visible = lblPayUnit.Visible = kind == "대출";
            numItemMonths.Enabled = chkBankPack.Checked && kind != "대출";
            lstGameItems.Items.Clear();
            foreach (var i in setupItems) lstGameItems.Items.Add(i.Line());
            // 미리 보기
            double take = Game.TakeHome((double)numSalary.Value * 1e4), living = (double)numLiving.Value * 1e4;
            double flow = chkSalaryPack.Checked ? take - living : (double)numGameMonthly.Value * 1e4;
            if (chkBankPack.Checked && chkSalaryPack.Checked)
                flow -= setupItems.Where(i => !i.Done && i.Kind != "예금").Sum(i => i.Monthly);
            lblGameFlow.ForeColor = flow >= 0 ? Theme.Good : Theme.Bad;
            lblGameFlow.Text = (chkSalaryPack.Checked ? $"월 실수령 약 {Krw(take)} − 생활비 {Krw(living)}" + (chkBankPack.Checked ? " − 대출 상환·적금 납입" : "") + " = " : "")
                               + $"매달 투자 계좌로 {(flow >= 0 ? "" : "−")}{Krw(Math.Abs(flow))}" + (flow < 0 ? "  (적자예요. 생활비나 상환액을 줄여 보세요)" : "");
        }
        foreach (var n in new[] { numSalary, numLiving, numGameMonthly }) n.ValueChanged += (_, _) => Sync();
        chkSalaryPack.CheckedChanged += (_, _) => Sync();
        chkBankPack.CheckedChanged += (_, _) => Sync();
        cmbItemKind.SelectedIndexChanged += (_, _) => Sync();

        var btnAddItem = Theme.Secondary("추가");
        var btnDelItem = Theme.Secondary("선택 빼기");
        btnAddItem.Click += (_, _) =>
        {
            string kind = (string)cmbItemKind.SelectedItem, name = txtItemName.Text.Trim();
            double amt = (double)numItemAmt.Value * 1e4;
            if (amt <= 0) { lblSetupNote.ForeColor = Theme.Bad; lblSetupNote.Text = "금액을 넣어 주세요."; return; }
            if (kind == "대출" && numItemPay.Value <= 0) { lblSetupNote.ForeColor = Theme.Bad; lblSetupNote.Text = "대출은 매달 갚는 돈(원금+이자)을 넣어 주세요."; return; }
            setupItems.Add(new FinItem
            {
                Kind = kind, Name = name == "" ? kind + " " + (setupItems.Count(i => i.Kind == kind) + 1) : name, Rate = (double)numItemRate.Value,
                Balance = kind == "적금" ? 0 : amt, Monthly = kind == "대출" ? (double)numItemPay.Value * 1e4 : kind == "적금" ? amt : 0,
                Months = kind == "대출" ? 0 : (int)numItemMonths.Value,
            });
            txtItemName.Text = "";
            lblSetupNote.Text = "";
            Sync();
        };
        btnDelItem.Click += (_, _) => { if (lstGameItems.SelectedIndex >= 0) { setupItems.RemoveAt(lstGameItems.SelectedIndex); Sync(); } };

        btnGameStart.Click += (_, _) =>
        {
            var packsOn = new List<string>();
            if (chkSalaryPack.Checked) packsOn.Add("salary");
            if (chkBankPack.Checked) packsOn.Add("bank");
            if (chkSalaryPack.Checked && numSalary.Value <= 0) { lblSetupNote.ForeColor = Theme.Bad; lblSetupNote.Text = "연봉·생활비 팩을 켰다면 연봉을 넣어 주세요."; return; }
            var f = new Game.Finance((double)numGameMonthly.Value * 1e4, packsOn, (double)numSalary.Value * 1e4, (double)numLiving.Value * 1e4,
                                     (double)numRaise.Value / 100, setupItems.Select(Clone).ToList());
            if (!Game.S.Started)
            {
                if (numGameSeed.Value <= 0) { lblSetupNote.ForeColor = Theme.Bad; lblSetupNote.Text = "시드머니를 넣어 주세요. 지금 실제로 투자에 쓸 수 있는 금액을 넣으면 가장 실감 나요."; return; }
                Game.Start((double)numGameSeed.Value * 1e4, f, Market.UsdKrw);
                gameDivs.Clear(); gamePrices.Clear();
                _ = GameTick(false);
            }
            else Game.SetFinance(f);
            ShowGameState();
        };
        btnSetupClose.Click += (_, _) => { gameStartPanel.Visible = false; };

        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var items = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(0, 2, 0, 6) };
        items.Controls.Add(lstGameItems);
        var rows = new Control[]
        {
            Note("처음 한 번 설정하고 시작해요. 나중에도 [내 정보]에서 언제든 바꿀 수 있어요. 앱을 꺼 둬도 시간은 흘러서, 다시 켜면 그사이 월급·배당·이자·만기·예약 주문을 처리해요."),
            Row(Theme.Text_("시드머니", Theme.Bold), numGameSeed, Theme.Text_("만원 (내가 정해요)      매달 투자금", Theme.Bold), numGameMonthly, Theme.Text_("만원 (연봉 팩을 끄면 이 금액)")),
            Row(chkSalaryPack, Theme.Text_("   연봉(세전)"), numSalary, Theme.Text_("만원   월 생활비"), numLiving, Theme.Text_("만원   해마다 1월 인상"), numRaise, Theme.Text_("%")),
            Note("   " + packs["salary"].Desc + " 실수령은 대략값이에요 (4대 보험·소득세, 부양가족 1명 기준)."),
            Row(chkBankPack, Theme.Text_("   "), cmbItemKind, txtItemName, numItemAmt, lblAmtUnit, Theme.Text_("연"), numItemRate, Theme.Text_("%"), numItemPay, lblPayUnit,
                numItemMonths, Theme.Text_("개월  "), btnAddItem, btnDelItem),
            Note("   " + packs["bank"].Desc + " 연봉 팩이 켜져 있으면 상환·납입금은 월급에서 나가요."),
            items,
            Row(lblGameFlow),
            Row(btnGameStart, btnSetupClose, lblSetupNote),
        };
        foreach (var r in rows.Reverse()) body.Controls.Add(r);
        Sync();
        gameSetupSync = Sync;
        return body;
    }

    Action gameSetupSync;
    static FinItem Clone(FinItem i) => new()
    {
        Kind = i.Kind, Name = i.Name, Balance = i.Balance, Rate = i.Rate, Monthly = i.Monthly, Months = i.Months, Elapsed = i.Elapsed, InterestPaid = i.InterestPaid, Done = i.Done,
    };

    // 내 정보 카드를 지금 게임 값으로 채워 연다 (게임 중) / 처음 설정용으로 연다
    void OpenGameSetup()
    {
        var st = Game.S;
        bool on = st.Started;
        numGameSeed.Enabled = !on;
        btnGameStart.Text = on ? "저장 (다음 달 1일부터 적용)" : "초기 설정 끝 · 게임 시작";
        btnSetupClose.Visible = on;
        if (on)
        {
            chkSalaryPack.Checked = GamePacks.On("salary");
            chkBankPack.Checked = GamePacks.On("bank");
            numSalary.Value = (decimal)Math.Min((double)numSalary.Maximum, st.Salary / 1e4);
            numLiving.Value = (decimal)Math.Min((double)numLiving.Maximum, st.Living / 1e4);
            numRaise.Value = (decimal)Math.Min((double)numRaise.Maximum, st.Raise * 100);
            numGameMonthly.Value = (decimal)Math.Min((double)numGameMonthly.Maximum, st.Monthly / 1e4);
            setupItems.Clear();
            setupItems.AddRange(st.Items.Select(Clone));
        }
        else
        {
            if (numGameMonthly.Value == 0) numGameMonthly.Value = Math.Min(numGameMonthly.Maximum, numMsMonthly.Value); // 전략기획실의 매달 금액을 기본으로
            if (numLiving.Value == 0 && Cart.Items.Count > 0) numLiving.Value = (decimal)Math.Min(100_000, Math.Round(Cart.Items.Sum(i => i.Monthly) / 1e4)); // 생활비 해금의 지출 합계
        }
        lblSetupNote.Text = "";
        gameSetupSync?.Invoke();
        gameStartPanel.Visible = true;
    }


    void ShowGameState()
    {
        bool on = Game.S.Started;
        if (on) gameStartPanel.Visible = false; else OpenGameSetup();
        gameMainPanel.Visible = on;
        UpdateGameClock();
        if (on) RefreshGame();
    }

    void UpdateGameClock()
    {
        var now = DateTime.Now;
        string Mk(string name, string cur) => Game.Open(cur, now) ? $"{name} 열림" : $"{name} 닫힘 (다음 {Game.NextOpen(cur, now):M/d HH:mm})";
        lblGameClock.Text = $"{now:yyyy-MM-dd (ddd) HH:mm:ss}   ·   {Mk("국장", "KRW")}   ·   {Mk("미장", "USD")}" +
                            (Game.S.Started ? $"   ·   {Game.S.Season}" : "");
    }

    static string GameSymbol(string code, string market, string currency) =>
        currency == "USD" ? code : code + (market == "KOSDAQ" ? ".KQ" : ".KS");

    double? GamePrice(string code)
    {
        if (gamePrices.TryGetValue(code, out var p) && p > 0) return p;
        if (rowByCode.TryGetValue(code, out var r) && r.Q.Price > 0) return r.Q.Price;
        return null;
    }

    // 보유·예약 종목의 지금 가격과 배당 기록을 받고, 지난번 이후 일어난 일을 처리한다
    async Task GameTick(bool welcome)
    {
        if (gameTicking || !Game.S.Started) return;
        gameTicking = true;
        gameLastTick = DateTime.Now;
        try
        {
            var items = Game.S.Holdings.Select(h => (h.Code, h.Market, h.Currency))
                        .Concat(Game.S.Pending.Select(o => (o.Code, o.Market, o.Currency))).Distinct().ToList();
            if (items.Count > 0)
            {
                try
                {
                    var q = await Yahoo.Quotes(items.Select(i => GameSymbol(i.Code, i.Market, i.Currency)));
                    foreach (var i in items)
                        if (q.TryGetValue(GameSymbol(i.Code, i.Market, i.Currency), out var v) && v.price > 0) gamePrices[i.Code] = v.price;
                }
                catch { } // 못 받으면 시세 목록 가격으로
                foreach (var h in Game.S.Holdings.Where(h => !gameDivs.ContainsKey(h.Code)).ToList())
                {
                    try { gameDivs[h.Code] = await Yahoo.DividendHistory(GameSymbol(h.Code, h.Market, h.Currency)); }
                    catch { } // 다음에 다시
                }
            }
            var news = Game.Tick(GamePrice, Market.UsdKrw, gameDivs);
            bool first = !gameSessionTicked;
            gameSessionTicked = true;
            if ((welcome || first) && news.Count > 0)
            {
                var counts = news.GroupBy(n => n.Kind).Select(g => $"{g.Key} {g.Count()}건");
                lblGameMsg.ForeColor = Color.FromArgb(250, 204, 21);
                lblGameMsg.Text = $"자리 비운 사이 소식: {string.Join(" · ", counts)}  — 오른쪽 소식에서 확인하세요";
            }
            RefreshGame();
        }
        catch (Exception ex) { lblGameMsg.ForeColor = Theme.Bad; lblGameMsg.Text = "처리하다 문제가 생겼어요: " + ex.Message.Split('\n')[0]; }
        finally { gameTicking = false; }
    }

    async Task GameOrder(bool buy)
    {
        if (gamePick == null) { lblGameMsg.ForeColor = Theme.Bad; lblGameMsg.Text = "먼저 종목을 찾아 고르세요."; return; }
        var r = gamePick;
        string sym = Yahoo.Symbol(r.Q);
        double? price = null;
        try { var q = await Yahoo.Quotes(new[] { sym }); if (q.TryGetValue(sym, out var v) && v.price > 0) price = gamePrices[r.Q.Code] = v.price; }
        catch { } // 못 받으면 시세 목록 가격
        price ??= GamePrice(r.Q.Code);
        var o = new KrxScreen.GameOrder
        {
            Code = r.Q.Code, Name = r.Q.Name, Market = r.Q.Market, Currency = r.Q.Currency, Etf = Scout.IsEtf(r), Buy = buy, Qty = (double)numGameQty.Value,
        };
        string msg = Game.Place(o, price, Market.UsdKrw);
        lblGameMsg.ForeColor = msg.Contains("체결") || msg.Contains("예약") ? Theme.Good : Theme.Bad;
        lblGameMsg.Text = msg;
        if (buy && !gameDivs.ContainsKey(r.Q.Code)) _ = GameTick(false); // 배당 기록 받기
        RefreshGame();
    }

    void RefreshGame()
    {
        var st = Game.S;
        if (!st.Started) return;
        double fx = Market.UsdKrw, value = Game.Value(GamePrice, fx), gain = value - st.Deposited;
        double bank = GamePacks.BankNet();
        if (GamePacks.On("bank") && st.Items.Count > 0)
            kGameTotal.Set(Krw(value + bank), null, $"투자 {Krw(value)} {(bank >= 0 ? "+" : "−")} 예적금·대출 {Krw(Math.Abs(bank))}", "순자산");
        else kGameTotal.Set(Krw(value), null, $"시작 {st.StartedAt:yyyy.MM.dd}", "총자산");
        kGameGain.Set((gain >= 0 ? "+" : "") + Krw(gain), gain > 0 ? Theme.Up : gain < 0 ? Theme.Down : null,
            $"{(st.Deposited > 0 ? gain / st.Deposited * 100 : 0):+0.0;-0.0;0.0}% · 넣은 돈 {Krw(st.Deposited)}");
        kGameCash.Set(Krw(st.Cash), null, st.Pending.Count > 0 ? $"예약 주문 {st.Pending.Count}건" : $"비용 누적 {Krw(st.FeesPaid)}");
        kGameDiv.Set(Krw(st.DivReceived), Color.FromArgb(52, 211, 153), "배당락일 기준으로 들어와요");
        double added = st.Deposited - st.SeasonStartDeposited;
        double sret = st.SeasonStartValue > 0 ? (value - added - st.SeasonStartValue) / st.SeasonStartValue * 100 : 0;
        kGameSeason.Set($"{sret:+0.0;-0.0;0.0}%", sret > 0 ? Theme.Up : sret < 0 ? Theme.Down : null, $"{st.Season} · 지금 등급 {Game.Grade(sret)}");

        var t = new DataTable();
        t.Columns.Add("종목"); t.Columns.Add("코드"); t.Columns.Add("포지션");
        foreach (var c in new[] { "수량", "평균단가(원)", "현재가", "평가금액(원)", "손익(원)", "손익%" }) t.Columns.Add(c, typeof(double));
        foreach (var h in st.Holdings.OrderByDescending(h => h.Qty * (GamePrice(h.Code) ?? 0) * Game.Fx(h.Currency, fx)))
        {
            double px = GamePrice(h.Code) ?? h.CostKrw / h.Qty / Game.Fx(h.Currency, fx);
            double ev = h.Qty * px * Game.Fx(h.Currency, fx);
            string pos = rowByCode.TryGetValue(h.Code, out var row) ? PosShort(row.Pos) : h.Etf ? "중원" : "";
            t.Rows.Add(h.Name, h.Code, pos, h.Qty, h.CostKrw / h.Qty, h.Currency == "USD" && !Won.UsdInKrw ? px : px * Game.Fx(h.Currency, fx), ev, ev - h.CostKrw,
                h.CostKrw > 0 ? (ev / h.CostKrw - 1) * 100 : 0);
        }
        gridGame.DataSource = t;
        FitColumns(gridGame, t, "종목");

        var sb = new StringBuilder();
        if (st.Pending.Count > 0)
        {
            sb.AppendLine("■ 예약 주문 (장이 열리면 체결)");
            foreach (var o in st.Pending) sb.AppendLine($"{o.Name} {o.Qty:N0}주 {(o.Buy ? "매수" : "매도")} · {Game.NextOpen(o.Currency, DateTime.Now):M/d HH:mm}");
            sb.AppendLine();
        }
        sb.AppendLine("■ 내 정보");
        sb.AppendLine(Game.FinanceLine());
        if (GamePacks.On("bank")) foreach (var i in st.Items) sb.AppendLine("• " + i.Line());
        sb.AppendLine();
        sb.AppendLine("■ 소식");
        foreach (var n in st.News.OrderByDescending(n => n.At).Take(80))
            sb.AppendLine($"{n.At:MM/dd HH:mm}  [{n.Kind}]  {n.Text}");
        sb.AppendLine();
        sb.AppendLine("국장 가격은 Yahoo 기준 약 20분, 미장은 약 15분 늦어요. 공휴일은 아직 빼지 않아요. 비용은 대략값이에요 (국내 수수료 0.015%, 팔 때 거래세 약 0.2%(ETF 제외), 미국 수수료 0.25% + 환전 0.1%).");
        txtGameNews.SetText(sb.ToString());
    }
}
