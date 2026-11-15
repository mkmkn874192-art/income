namespace KrxScreen;

// 종목 검색칸: 이름·코드를 치면 아래에 후보가 뜨고, 클릭하거나 Enter로 고르면 Picked가 불린다.
// 후보 목록은 창(Form) 위에 띄워서 카드나 표에 가려지지 않게 한다.
class StockPicker : Panel
{
    readonly TextBox box = new() { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Font = Theme.Body, BackColor = Theme.InputBg, ForeColor = Theme.Text };
    readonly ListBox list = new()
    {
        BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.CardBg, ForeColor = Theme.Text, Font = Theme.Body,
        IntegralHeight = false, Visible = false, DrawMode = DrawMode.OwnerDrawFixed,
    };
    readonly Func<IEnumerable<StockRow>> rows;
    public event Action<StockRow> Picked;

    public StockPicker(Func<IEnumerable<StockRow>> rows, string placeholder)
    {
        this.rows = rows;
        BackColor = Theme.InputBg;
        Height = 34;
        Padding = new Padding(14, 8, 10, 4);
        Margin = new Padding(0, 0, 8, 0);
        DoubleBuffered = true;
        box.PlaceholderText = placeholder;
        box.AccessibleName = placeholder; // 화면 읽기 프로그램용 이름
        Controls.Add(box);

        box.TextChanged += (_, _) => Fill();
        box.GotFocus += (_, _) => Invalidate();
        box.KeyDown += (_, e) =>
        {
            if (!list.Visible) return;
            if (e.KeyCode == Keys.Down) { list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Pick(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { list.Visible = false; e.Handled = true; }
        };
        // 목록을 누르는 사이에 칸이 포커스를 잃으므로 조금 늦게 닫는다
        box.LostFocus += (_, _) => BeginInvoke(() => { if (!list.Focused) list.Visible = false; Invalidate(); });
        list.MouseClick += (_, _) => Pick();
        list.LostFocus += (_, _) => list.Visible = false;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var r = (StockRow)list.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? Theme.AccentSoft : Theme.CardBg)) e.Graphics.FillRectangle(bg, e.Bounds);
            var b = e.Bounds;
            TextRenderer.DrawText(e.Graphics, r.Q.Name, Theme.Bold, new Rectangle(b.X + 10, b.Y, b.Width / 2, b.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string price = Won.Price(r.Q.Price, r.Q.Currency);
            TextRenderer.DrawText(e.Graphics, $"{r.Q.Code} · {(r.Q.IsUs ? "미국" : r.Q.Market)} · {price}", Theme.Small,
                new Rectangle(b.X + b.Width / 2, b.Y, b.Width / 2 - 10, b.Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        };
    }

    void Fill()
    {
        string q = box.Text.Trim();
        var found = q.Length == 0 ? new List<StockRow>() : Labels.Search(rows(), q, out _).Take(10).ToList();
        if (found.Count == 0) { list.Visible = false; return; }
        var form = FindForm();
        if (form == null) return;
        if (list.Parent != form) { form.Controls.Add(list); list.ItemHeight = (int)(30 * DeviceDpi / 96f); }
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var r in found) list.Items.Add(r);
        list.SelectedIndex = 0;
        list.EndUpdate();
        var p = form.PointToClient(PointToScreen(new Point(0, Height + 2)));
        int w = Math.Max(Width, (int)(420 * DeviceDpi / 96f));
        p.X = Math.Min(p.X, form.ClientSize.Width - w - 4);
        list.Bounds = new Rectangle(p.X, p.Y, w, list.ItemHeight * found.Count + 4);
        list.Visible = true;
        list.BringToFront();
    }

    void Pick()
    {
        if (list.SelectedItem is not StockRow r) return;
        list.Visible = false;
        box.Text = "";
        Picked?.Invoke(r);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f);
        using (var fill = new SolidBrush(Theme.InputBg)) g.FillPath(fill, path);
        using var pen = new Pen(box.Focused ? Theme.Accent : Theme.Border);
        g.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}
