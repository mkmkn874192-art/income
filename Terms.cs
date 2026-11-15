namespace KrxScreen;

// 글 속의 용어를 파란 밑줄로 표시하고, 클릭하면 그 자리에 설명 창을 띄우는 읽기 전용 글상자.
class TermText : RichTextBox
{
    // 글에서 찾을 용어 (너무 흔한 말은 빼서 글이 파랗게 도배되지 않게)
    static readonly string[] Stop = { "지수", "점수", "업종", "세금", "분산", "국면", "시총", "개별주", "포트폴리오", "세후", "환율", "채권", "역전", "고점", "흐름", "감독", "코치", "코치진", "포지션", "별점" };
    static readonly List<(string word, Glossary.Entry e)> words = Glossary.All
        .SelectMany(e => e.Aliases.Append(e.Key).Select(a => (a, e)))
        .Where(x => x.a.Length >= 2 && !Stop.Contains(x.a))
        .GroupBy(x => x.a).Select(g => g.First())
        .OrderByDescending(x => x.a.Length).ToList(); // 긴 말 먼저 ("PER(3년)"이 "PER"보다 먼저)

    readonly List<(int start, int len, Glossary.Entry e)> links = new();
    public Action<Glossary.Entry> OpenFull; // 설명 창의 [배우기에서 전체 보기]

    public TermText()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.CardBg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        DetectUrls = false;
        ScrollBars = RichTextBoxScrollBars.Vertical;
        Cursor = Cursors.Default;
        Theme.DarkScrollbars(this);
        MouseMove += (_, e) => Cursor = At(e.Location) != null ? Cursors.Hand : Cursors.Default;
        MouseUp += (_, e) =>
        {
            if (At(e.Location) is Glossary.Entry en) TermPopup.Show(en, PointToScreen(e.Location), OpenFull);
        };
    }

    Glossary.Entry At(Point p)
    {
        int i = GetCharIndexFromPosition(p);
        // 글 끝보다 아래를 가리키면 마지막 글자로 잡히므로 실제 위치를 한 번 더 확인
        if (i < 0 || i >= TextLength || Math.Abs(GetPositionFromCharIndex(i).Y - p.Y) > Font.Height * 1.5) return null;
        foreach (var (start, len, e) in links) if (i >= start && i < start + len) return e;
        return null;
    }

    public void SetText(string text, Color? color = null)
    {
        Text = text;
        SelectAll();
        SelectionColor = color ?? Theme.Text;
        SelectionFont = Font;
        links.Clear();
        var taken = new bool[text.Length];
        using var link = new Font(Font, FontStyle.Underline);
        foreach (var (word, e) in words)
        {
            for (int i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + word.Length, StringComparison.Ordinal))
            {
                if (Enumerable.Range(i, word.Length).Any(k => taken[k])) continue;
                // 영문 용어는 단어 중간(예: "PERIOD")은 건너뛴다
                if (char.IsAsciiLetter(word[0]) && i > 0 && char.IsAsciiLetter(text[i - 1])) continue;
                for (int k = i; k < i + word.Length; k++) taken[k] = true;
                links.Add((i, word.Length, e));
                Select(i, word.Length);
                SelectionColor = Color.FromArgb(96, 165, 250);
                SelectionFont = link;
            }
        }
        Select(0, 0);
    }
}

// 용어 설명 창. 다른 곳을 클릭하면 닫힌다.
class TermPopup : Form
{
    static TermPopup current;
    readonly Label title = new() { Dock = DockStyle.Top, Height = 34, Font = Theme.H2, ForeColor = Theme.Text, AutoEllipsis = true };
    readonly Label term = new() { Dock = DockStyle.Top, Height = 22, Font = Theme.Small, ForeColor = Theme.Muted, AutoEllipsis = true };
    readonly Label summary = new() { Dock = DockStyle.Top, Height = 52, Font = Theme.Bold, ForeColor = Color.FromArgb(96, 165, 250) };
    readonly TermText body = new() { Dock = DockStyle.Fill };
    readonly Button full = Theme.Secondary("자료실에서 전체 보기");
    Glossary.Entry entry;
    Action<Glossary.Entry> openFull;

    TermPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Theme.CardBg;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(480, 380);
        Padding = new Padding(16, 12, 16, 12);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        bottom.Controls.Add(full);
        Controls.Add(body);
        Controls.Add(bottom);
        Controls.Add(summary);
        Controls.Add(term);
        Controls.Add(title);
        full.Click += (_, _) => { var e = entry; var f = openFull; Close(); f?.Invoke(e); };
        body.OpenFull = e => { var f = openFull; Close(); f?.Invoke(e); };
        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Accent);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    void Fill(Glossary.Entry e)
    {
        entry = e;
        title.Text = e.Title;
        term.Text = e.Term;
        summary.Text = e.Short;
        // 설명 창 안의 용어를 누르면 같은 창 내용이 바뀐다
        body.OpenFull = x => { var f = openFull; Close(); f?.Invoke(x); };
        body.SetText(string.Join("\n", e.Body.Split('\n').Select(l => l.TrimEnd('\r')).Select(l => l.StartsWith("## ") ? "\n■ " + l[3..] : l)).Trim());
    }

    public static void Show(Glossary.Entry e, Point screen, Action<Glossary.Entry> openFull)
    {
        if (current is { IsDisposed: false })
        {
            // 이미 떠 있으면 내용만 바꾼다 (설명 속 용어를 눌렀을 때)
            current.openFull = openFull;
            current.Fill(e);
            return;
        }
        var p = new TermPopup { openFull = openFull };
        p.Fill(e);
        var area = Screen.FromPoint(screen).WorkingArea;
        p.Location = new Point(Math.Min(screen.X + 12, area.Right - p.Width), Math.Min(screen.Y + 12, area.Bottom - p.Height));
        p.FormClosed += (_, _) => current = null;
        current = p;
        p.Show(Form.ActiveForm);
    }
}
