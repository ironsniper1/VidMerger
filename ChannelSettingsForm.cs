namespace VidMerger;

public class ChannelSettingsForm : Form
{
    private readonly AppSettings _settings;

    private ListBox _lbYt = null!;
    private TextBox _txtYtName = null!, _txtYtUrl = null!, _txtYtClientId = null!, _txtYtClientSecret = null!;
    private Button _btnYtAdd = null!, _btnYtUpdate = null!, _btnYtRemove = null!, _btnYtSetActive = null!;
    private Label _lblYtActive = null!;
    private NumericUpDown _nudBid = null!;

    public ChannelSettingsForm(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadAll();
    }

    private void InitializeComponent()
    {
        Text = "Channel Settings";
        Size = new Size(700, 520);
        MinimumSize = new Size(600, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false; MinimizeBox = false;

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildYouTubeTab());
        tabs.TabPages.Add(BuildLbryTab());
        outer.Controls.Add(tabs, 0, 0);

        var closeBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 6, 8, 6)
        };
        var btnClose = new Button { Text = "Close", Width = 90, Height = 28, DialogResult = DialogResult.OK };
        closeBar.Controls.Add(btnClose);
        outer.Controls.Add(closeBar, 0, 1);

        Controls.Add(outer);
        AcceptButton = btnClose;
    }

    // ── YouTube tab ───────────────────────────────────────────────────────

    private TabPage BuildYouTubeTab()
    {
        var tab = new TabPage("YouTube");
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(8)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lbYt = new ListBox { Dock = DockStyle.Fill };
        _lbYt.SelectedIndexChanged += OnYtSelectionChanged;
        split.Controls.Add(_lbYt, 0, 0);

        var stack = MakeStack();

        _txtYtName         = MakeField("e.g. Main Channel");
        _txtYtUrl          = MakeField("https://www.youtube.com/@YourChannel/videos");
        _txtYtClientId     = MakeField();
        _txtYtClientSecret = MakeField(password: true);

        _btnYtAdd       = MakeBtn("Add");    _btnYtAdd.Click    += OnYtAdd;
        _btnYtUpdate    = MakeBtn("Update"); _btnYtUpdate.Click += OnYtUpdate; _btnYtUpdate.Enabled = false;
        _btnYtRemove    = MakeBtn("Remove"); _btnYtRemove.Click += OnYtRemove; _btnYtRemove.Enabled = false;
        _btnYtSetActive = new Button
        {
            Text = "✓ Set as Active Channel", Height = 28,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            BackColor = Color.FromArgb(220, 240, 220),
            Margin = new Padding(0, 4, 0, 4), Enabled = false
        };
        _btnYtSetActive.Click += OnYtSetActive;
        _lblYtActive = new Label { AutoSize = false, Height = 36, ForeColor = Color.DarkGreen, Margin = new Padding(0, 0, 0, 4) };

        var chanBtnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 4) };
        chanBtnRow.Controls.AddRange(new Control[] { _btnYtAdd, _btnYtUpdate, _btnYtRemove });

        var btnSaveApi = MakeBtn("Save API"); btnSaveApi.Width = 90; btnSaveApi.Margin = new Padding(0, 8, 0, 0);
        btnSaveApi.Click += (_, _) =>
        {
            _settings.YouTubeClientId     = _txtYtClientId.Text.Trim();
            _settings.YouTubeClientSecret = _txtYtClientSecret.Text.Trim();
            _settings.Save();
            MessageBox.Show("API credentials saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var widthTargets = new List<Control> { _btnYtSetActive, _lblYtActive, chanBtnRow };

        stack.Controls.Add(SectionLbl("── Channel ──"));
        AddField(stack, "Name:", _txtYtName, widthTargets);
        AddField(stack, "Channel URL:", _txtYtUrl, widthTargets);
        stack.Controls.Add(chanBtnRow);
        stack.Controls.Add(_btnYtSetActive);
        stack.Controls.Add(_lblYtActive);
        stack.Controls.Add(SectionLbl("── Upload API  (optional — only needed to upload TO YouTube) ──"));
        AddField(stack, "Client ID:", _txtYtClientId, widthTargets);
        AddField(stack, "Client Secret:", _txtYtClientSecret, widthTargets);
        stack.Controls.Add(btnSaveApi);

        BindWidths(stack, widthTargets.ToArray());

        split.Controls.Add(MakeScrollPane(stack), 1, 0);
        tab.Controls.Add(split);
        return tab;
    }

    // ── LBRY tab ──────────────────────────────────────────────────────────

    private TabPage BuildLbryTab()
    {
        var tab = new TabPage("LBRY");
        var stack = MakeStack();

        var bidLbl = new Label { Text = "Default Bid (LBC):", AutoSize = false, Height = 20, Width = 200, Margin = new Padding(0, 4, 0, 0) };
        _nudBid = new NumericUpDown
        {
            Width = 120, Height = 24, DecimalPlaces = 4,
            Minimum = 0.0001M, Maximum = 1000M, Increment = 0.0001M,
            Margin = new Padding(0, 2, 0, 0)
        };
        var btnSave = MakeBtn("Save"); btnSave.Margin = new Padding(0, 8, 0, 0);
        btnSave.Click += (_, _) =>
        {
            _settings.LbryDefaultBid = (double)_nudBid.Value;
            _settings.Save();
            MessageBox.Show("Saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        stack.Controls.Add(SectionLbl("LBRY connects via the local LBRY Desktop app — no API key needed."));
        stack.Controls.Add(bidLbl);
        stack.Controls.Add(_nudBid);
        stack.Controls.Add(btnSave);

        tab.Controls.Add(MakeScrollPane(stack));
        return tab;
    }

    // ── Load ─────────────────────────────────────────────────────────────

    private void LoadAll()
    {
        RefreshYtList(); UpdateYtActiveLabel();
        _txtYtClientId.Text     = _settings.YouTubeClientId;
        _txtYtClientSecret.Text = _settings.YouTubeClientSecret;
        _nudBid.Value           = (decimal)Math.Max(0.0001, _settings.LbryDefaultBid);
    }

    // ── YouTube handlers ──────────────────────────────────────────────────

    private void RefreshYtList()
    {
        _lbYt.Items.Clear();
        foreach (var ch in _settings.YouTubeChannels)
            _lbYt.Items.Add(ch.Url == _settings.ActiveChannelUrl ? $"★ {ch.Name}" : $"   {ch.Name}");
    }

    private void UpdateYtActiveLabel()
    {
        var a = _settings.ActiveChannel;
        _lblYtActive.Text = a != null ? $"Active: {a.Name}\n{a.Url}" : "No active channel set.";
    }

    private void OnYtSelectionChanged(object? sender, EventArgs e)
    {
        int i = _lbYt.SelectedIndex;
        if (i < 0 || i >= _settings.YouTubeChannels.Count)
        { _btnYtUpdate.Enabled = _btnYtRemove.Enabled = _btnYtSetActive.Enabled = false; return; }
        var ch = _settings.YouTubeChannels[i];
        _txtYtName.Text = ch.Name; _txtYtUrl.Text = ch.Url;
        _btnYtUpdate.Enabled = _btnYtRemove.Enabled = true;
        _btnYtSetActive.Enabled = ch.Url != _settings.ActiveChannelUrl;
    }

    private void OnYtAdd(object? sender, EventArgs e)
    {
        string name = _txtYtName.Text.Trim(), url = _txtYtUrl.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) { Warn(); return; }
        _settings.YouTubeChannels.Add(new YouTubeChannel { Name = name, Url = url });
        if (_settings.YouTubeChannels.Count == 1) _settings.ActiveChannelUrl = url;
        _settings.Save(); RefreshYtList(); UpdateYtActiveLabel(); _txtYtName.Clear(); _txtYtUrl.Clear();
    }

    private void OnYtUpdate(object? sender, EventArgs e)
    {
        int i = _lbYt.SelectedIndex; if (i < 0) return;
        string name = _txtYtName.Text.Trim(), url = _txtYtUrl.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) { Warn(); return; }
        if (_settings.YouTubeChannels[i].Url == _settings.ActiveChannelUrl) _settings.ActiveChannelUrl = url;
        _settings.YouTubeChannels[i] = new YouTubeChannel { Name = name, Url = url };
        _settings.Save(); RefreshYtList(); UpdateYtActiveLabel(); _lbYt.SelectedIndex = i;
    }

    private void OnYtRemove(object? sender, EventArgs e)
    {
        int i = _lbYt.SelectedIndex; if (i < 0) return;
        if (MessageBox.Show($"Remove \"{_settings.YouTubeChannels[i].Name}\"?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (_settings.ActiveChannelUrl == _settings.YouTubeChannels[i].Url) _settings.ActiveChannelUrl = "";
        _settings.YouTubeChannels.RemoveAt(i); _settings.Save(); RefreshYtList(); UpdateYtActiveLabel();
        _txtYtName.Clear(); _txtYtUrl.Clear(); _btnYtUpdate.Enabled = _btnYtRemove.Enabled = _btnYtSetActive.Enabled = false;
    }

    private void OnYtSetActive(object? sender, EventArgs e)
    {
        int i = _lbYt.SelectedIndex; if (i < 0) return;
        _settings.ActiveChannelUrl = _settings.YouTubeChannels[i].Url;
        _settings.Save(); RefreshYtList(); UpdateYtActiveLabel(); _btnYtSetActive.Enabled = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static FlowLayoutPanel MakeStack() => new FlowLayoutPanel
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,       // grows to fit content
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(8)
    };

    // Wraps a stack in a scrollable panel — use this as the right-side control
    private static Panel MakeScrollPane(FlowLayoutPanel stack)
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        stack.Dock = DockStyle.None;  // let AutoSize control dimensions
        scroll.Controls.Add(stack);
        // Keep stack width in sync with the scroll pane (minus scrollbar)
        scroll.Resize += (_, _) =>
        {
            int w = scroll.ClientSize.Width - 2;
            stack.MaximumSize = new Size(w, 0);
            stack.MinimumSize = new Size(w, 0);
        };
        return scroll;
    }

    private static Label SectionLbl(string text) => new Label
    {
        Text = text, AutoSize = false, Height = 24,
        ForeColor = Color.Gray, Font = new Font(SystemFonts.DefaultFont, FontStyle.Italic),
        TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 10, 0, 2),
        Width = 400  // default; overridden by BindWidths via stack.Resize
    };

    private static TextBox MakeField(string placeholder = "", bool password = false) => new TextBox
        { Height = 24, PlaceholderText = placeholder, UseSystemPasswordChar = password, Margin = new Padding(0, 2, 0, 2) };

    private static Button MakeBtn(string text) =>
        new Button { Text = text, Width = 84, Height = 26, Margin = new Padding(0, 0, 4, 0) };

    private static void AddField(FlowLayoutPanel stack, string labelText, TextBox box, List<Control> widthTargets)
    {
        var lbl = new Label
        {
            Text = labelText, AutoSize = false, Height = 20,
            TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 6, 0, 0)
        };
        stack.Controls.Add(lbl);
        stack.Controls.Add(box);
        widthTargets.Add(lbl);
        widthTargets.Add(box);
    }

    private static void BindWidths(FlowLayoutPanel stack, Control[] controls)
    {
        void Apply(int w)
        {
            int target = Math.Max(100, w - 4);
            foreach (var c in controls) c.Width = target;
            foreach (Control c in stack.Controls)
                if (c is Label { ForeColor: var fc } lbl && fc == Color.Gray) lbl.Width = target;
        }
        // Width is driven by MakeScrollPane's Resize event via MinimumSize/MaximumSize
        // but we still need an initial pass and a way to respond to stack width changes
        stack.SizeChanged += (_, _) => Apply(stack.Width - stack.Padding.Horizontal);
        Apply(Math.Max(100, stack.Width - stack.Padding.Horizontal));
    }

    private static void Warn() =>
        MessageBox.Show("Please enter both a name and a URL.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
