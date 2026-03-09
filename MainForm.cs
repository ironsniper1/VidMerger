namespace VidMerger;

public class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly YouTubeService _youTube;
    private readonly LbryService _lbry;

    private List<VideoItem> _ytVideos = new();
    private List<VideoItem> _lbryVideos = new();
    private List<VideoItem> _missingFromLbry = new();
    private List<VideoItem> _missingFromYt = new();

    private CancellationTokenSource? _syncCts;

    // Status row controls
    private Label _lblYtStatus = null!;
    private Label _lblLbryStatus = null!;
    private Button _btnYtLoad = null!;
    private Button _btnYtEnableUpload = null!;
    private Button _btnLbryCheck = null!;
    private Button _btnLbryLoad = null!;

    // Missing from LBRY panel
    private ListBox _lbMissingLbry = null!;
    private Label _lblMissingLbryCount = null!;

    // Missing from YouTube panel
    private ListBox _lbMissingYt = null!;
    private Label _lblMissingYtCount = null!;
    private Button _btnUploadToYt = null!;

    // Status strip
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripProgressBar _progressBar = null!;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _youTube  = new YouTubeService(_settings);
        _lbry     = new LbryService(_settings);
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "VidMerger v1.0";
        // App icon
        string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
            Icon = new Icon(icoPath);
        Size = new Size(900, 700);
        MinimumSize = new Size(700, 550);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Toolbar ──────────────────────────────────────────────────────
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        var btnSettings = new ToolStripButton("⚙  Channel Settings")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        btnSettings.Click += OnChannelSettings;
        toolbar.Items.Add(btnSettings);
        Controls.Add(toolbar);

        // ── Status strip ─────────────────────────────────────────────────
        var statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _progressBar = new ToolStripProgressBar { Visible = false, Width = 200 };
        statusStrip.Items.Add(_statusLabel);
        statusStrip.Items.Add(_progressBar);
        Controls.Add(statusStrip);

        // ── Main layout: status row on top, two panels below ─────────────
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        main.Controls.Add(BuildStatusRow(), 0, 0);
        main.Controls.Add(BuildMissingPanels(), 0, 1);

        Controls.Add(main);
        main.BringToFront();
        statusStrip.BringToFront();
    }

    // ================================================================== //
    // Status row — YouTube + LBRY side by side
    // ================================================================== //

    private Panel BuildStatusRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var ytBox = new GroupBox { Text = "YouTube", Left = 0, Top = 0, Width = 380, Height = 80 };
        _lblYtStatus = new Label { Left = 8, Top = 18, Width = 360, Height = 20, ForeColor = Color.Gray, Text = "Not loaded" };
        _btnYtLoad = new Button { Text = "Load Videos", Left = 8, Top = 44, Width = 90, Height = 26 };
        _btnYtEnableUpload = new Button
        {
            Text = "Enable Upload API", Left = 106, Top = 44, Width = 130, Height = 26,
            Visible = !string.IsNullOrWhiteSpace(_settings.YouTubeClientId),
            Font = new Font(Font, FontStyle.Italic)
        };
        _btnYtLoad.Click += OnYtLoad;
        _btnYtEnableUpload.Click += OnYtEnableUpload;
        ytBox.Controls.AddRange(new Control[] { _lblYtStatus, _btnYtLoad, _btnYtEnableUpload });
        UpdateYtStatusLabel();

        var lbryBox = new GroupBox { Text = "LBRY", Left = 392, Top = 0, Width = 380, Height = 80 };
        _lblLbryStatus = new Label { Left = 8, Top = 18, Width = 360, Height = 20, ForeColor = Color.Gray, Text = "Not connected" };
        _btnLbryCheck = new Button { Text = "Connect", Left = 8, Top = 44, Width = 80, Height = 26 };
        _btnLbryLoad  = new Button { Text = "Load Videos", Left = 96, Top = 44, Width = 90, Height = 26, Enabled = false };
        _btnLbryCheck.Click += OnLbryCheck;
        _btnLbryLoad.Click  += OnLbryLoad;
        lbryBox.Controls.AddRange(new Control[] { _lblLbryStatus, _btnLbryCheck, _btnLbryLoad });

        panel.Controls.Add(ytBox);
        panel.Controls.Add(lbryBox);
        return panel;
    }

    // ================================================================== //
    // Two missing-video panels side by side
    // ================================================================== //

    private Control BuildMissingPanels()
    {
        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 1,
            Padding = new Padding(0)
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tbl.Controls.Add(BuildMissingFromLbryPanel(), 0, 0);
        tbl.Controls.Add(BuildMissingFromYtPanel(),  1, 0);
        return tbl;
    }

    // Each panel: title label, count label, listbox, then a FlowLayoutPanel
    // of buttons that AutoScroll handles if the window is too small.
    private Panel BuildMissingFromLbryPanel()
    {
        Button? syncBtn = null, cancelBtn = null;

        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // title
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // count
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // listbox
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // buttons

        var lblTitle = new Label
        {
            Text = "Missing from LBRY", Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold)
        };
        _lblMissingLbryCount = new Label
        {
            Text = "Run Compare to find videos on YouTube but not LBRY",
            Dock = DockStyle.Fill, ForeColor = Color.DarkRed
        };
        _lbMissingLbry = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

        // Buttons in a FlowLayoutPanel — wraps naturally, fixed height row gives room
        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 2, 0, 2)
        };

        var btnCompare  = new Button { Text = "Compare",        Width = 80,  Height = 26, Margin = new Padding(0, 0, 3, 3) };
        var btnDownload = new Button { Text = "Download",       Width = 80,  Height = 26, Margin = new Padding(0, 0, 3, 3) };
        var btnUpload   = new Button { Text = "Upload to LBRY", Width = 105, Height = 26, Margin = new Padding(0, 0, 3, 3) };
        syncBtn         = new Button { Text = "⟳ Sync All",    Width = 85,  Height = 26, Margin = new Padding(0, 0, 3, 3), BackColor = Color.FromArgb(220, 235, 255) };
        cancelBtn       = new Button { Text = "Cancel",         Width = 70,  Height = 26, Margin = new Padding(0, 0, 3, 3), Visible = false };
        var btnRemove   = new Button { Text = "Remove",         Width = 70,  Height = 26, Margin = new Padding(0, 0, 3, 3) };

        btnCompare.Click  += OnShowMissingLbry;
        btnDownload.Click += OnDownloadForLbry;
        btnUpload.Click   += OnUploadToLbry;
        syncBtn.Click     += (s, e) => OnSyncAll(_missingFromLbry, _lbMissingLbry, _lblMissingLbryCount, SyncTarget.LBRY, syncBtn, cancelBtn);
        cancelBtn.Click   += (_, _) => _syncCts?.Cancel();
        btnRemove.Click   += (_, _) => RemoveSelected(_lbMissingLbry, _missingFromLbry, _lblMissingLbryCount);

        btnFlow.Controls.AddRange(new Control[] { btnCompare, btnDownload, btnUpload, syncBtn, cancelBtn, btnRemove });

        layout.Controls.Add(lblTitle,             0, 0);
        layout.Controls.Add(_lblMissingLbryCount, 0, 1);
        layout.Controls.Add(_lbMissingLbry,       0, 2);
        layout.Controls.Add(btnFlow,              0, 3);

        outer.Controls.Add(layout);
        return outer;
    }

    private Panel BuildMissingFromYtPanel()
    {
        Button? syncBtn = null, cancelBtn = null;

        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        var lblTitle = new Label
        {
            Text = "Missing from YouTube", Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold)
        };
        _lblMissingYtCount = new Label
        {
            Text = "Run Compare to find videos on LBRY but not YouTube",
            Dock = DockStyle.Fill, ForeColor = Color.DarkRed
        };
        _lbMissingYt = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 2, 0, 2)
        };

        var btnCompare  = new Button { Text = "Compare",      Width = 80, Height = 26, Margin = new Padding(0, 0, 3, 3) };
        var btnDownload = new Button { Text = "Download",     Width = 80, Height = 26, Margin = new Padding(0, 0, 3, 3) };
        _btnUploadToYt  = new Button { Text = "Upload to YT", Width = 95, Height = 26, Margin = new Padding(0, 0, 3, 3), Enabled = false };
        syncBtn         = new Button { Text = "⟳ Sync All",  Width = 85, Height = 26, Margin = new Padding(0, 0, 3, 3), BackColor = Color.FromArgb(220, 235, 255) };
        cancelBtn       = new Button { Text = "Cancel",       Width = 70, Height = 26, Margin = new Padding(0, 0, 3, 3), Visible = false };
        var btnRemove   = new Button { Text = "Remove",       Width = 70, Height = 26, Margin = new Padding(0, 0, 3, 3) };

        btnCompare.Click     += OnShowMissingYt;
        btnDownload.Click    += OnDownloadForYt;
        _btnUploadToYt.Click += OnUploadToYt;
        syncBtn.Click        += (s, e) => OnSyncAll(_missingFromYt, _lbMissingYt, _lblMissingYtCount, SyncTarget.YouTube, syncBtn, cancelBtn);
        cancelBtn.Click      += (_, _) => _syncCts?.Cancel();
        btnRemove.Click      += (_, _) => RemoveSelected(_lbMissingYt, _missingFromYt, _lblMissingYtCount);

        btnFlow.Controls.AddRange(new Control[] { btnCompare, btnDownload, _btnUploadToYt, syncBtn, cancelBtn, btnRemove });

        layout.Controls.Add(lblTitle,           0, 0);
        layout.Controls.Add(_lblMissingYtCount, 0, 1);
        layout.Controls.Add(_lbMissingYt,       0, 2);
        layout.Controls.Add(btnFlow,            0, 3);

        outer.Controls.Add(layout);
        return outer;
    }

    // ================================================================== //
    // Sync All
    // ================================================================== //

    private enum SyncTarget { LBRY, YouTube }

    private async void OnSyncAll(
        List<VideoItem> list, ListBox lb, Label countLabel,
        SyncTarget target, Button btnSync, Button btnCancel)
    {
        if (list.Count == 0)
        {
            MessageBox.Show("Nothing to sync. Run Compare first.", "Empty list", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (target == SyncTarget.YouTube && !_youTube.CanUpload)
        {
            MessageBox.Show("YouTube upload requires API credentials. Use 'Enable Upload API' first.",
                "Not authenticated", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _syncCts = new CancellationTokenSource();
        btnSync.Enabled = false; btnCancel.Visible = true;
        _progressBar.Visible = true; _progressBar.Minimum = 0;
        _progressBar.Maximum = list.Count; _progressBar.Value = 0;

        string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videos");
        var toSync = list.ToList();
        int succeeded = 0, failed = 0;

        foreach (var video in toSync)
        {
            if (_syncCts.Token.IsCancellationRequested) { SetStatus("Sync cancelled."); break; }
            SetStatus($"[{succeeded + failed + 1}/{toSync.Count}] Downloading: {video.Title}");
            try
            {
                var progress = new Progress<string>(msg => SetStatus($"[{succeeded + failed + 1}/{toSync.Count}] {msg}"));
                string? filePath = target == SyncTarget.LBRY
                    ? await _youTube.DownloadVideoAsync(video, outputFolder, progress)
                    : await _lbry.DownloadVideoAsync(video, outputFolder, progress);

                if (string.IsNullOrEmpty(filePath)) { failed++; _progressBar.Value++; continue; }
                if (_syncCts.Token.IsCancellationRequested) break;

                SetStatus($"[{succeeded + failed + 1}/{toSync.Count}] Uploading: {video.Title}");
                bool ok = target == SyncTarget.LBRY
                    ? await _lbry.UploadVideoAsync(video, filePath, video.LocalThumbnailPath, _settings.LbryDefaultBid, progress)
                    : await _youTube.UploadVideoAsync(video, filePath, video.LocalThumbnailPath, progress);

                if (ok)
                {
                    succeeded++;
                    list.Remove(video);
                    RefreshList(lb, list);
                    countLabel.Text = $"{list.Count} video(s) remaining";
                    if (target == SyncTarget.LBRY) _lbryVideos.Add(video);
                    else _ytVideos.Add(video);
                }
                else failed++;
            }
            catch (Exception ex) { failed++; SetStatus($"Error: {ex.Message}"); }
            _progressBar.Value = Math.Min(_progressBar.Value + 1, _progressBar.Maximum);
        }

        btnSync.Enabled = true; btnCancel.Visible = false; _progressBar.Visible = false;
        string summary = $"Sync complete — {succeeded} succeeded, {failed} failed.";
        SetStatus(summary);
        MessageBox.Show(summary, "Sync Done", MessageBoxButtons.OK,
            failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    // ================================================================== //
    // Status label helpers
    // ================================================================== //

    private void UpdateYtStatusLabel(int? count = null)
    {
        var active = _settings.ActiveChannel;
        if (count.HasValue)
        {
            _lblYtStatus.Text = $"{active?.Name ?? "YouTube"} — {count} videos loaded";
            _lblYtStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblYtStatus.Text = active != null ? $"Channel: {active.Name}  (not loaded)" : "No channel set — click ⚙ Channel Settings";
            _lblYtStatus.ForeColor = active != null ? Color.DarkBlue : Color.Gray;
        }
    }

    // ================================================================== //
    // Channel Settings
    // ================================================================== //

    private void OnChannelSettings(object? sender, EventArgs e)
    {
        using var form = new ChannelSettingsForm(_settings);
        form.ShowDialog(this);
        UpdateYtStatusLabel(_ytVideos.Count > 0 ? _ytVideos.Count : null);
        _btnYtEnableUpload.Visible = !string.IsNullOrWhiteSpace(_settings.YouTubeClientId);
    }

    // ================================================================== //
    // YouTube handlers
    // ================================================================== //

    private async void OnYtLoad(object? sender, EventArgs e)
    {
        if (_settings.ActiveChannel == null)
        {
            MessageBox.Show("No YouTube channel set. Add one in ⚙ Channel Settings first.", "No channel",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            OnChannelSettings(null, EventArgs.Empty);
            if (_settings.ActiveChannel == null) return;
        }
        _btnYtLoad.Enabled = false;
        _ytVideos.Clear();
        SetStatus($"Loading {_settings.ActiveChannel.Name}...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            _ytVideos = await _youTube.GetChannelVideosAsync(_settings.ActiveChannel.Url, progress);
            UpdateYtStatusLabel(_ytVideos.Count);
            SetStatus($"Loaded {_ytVideos.Count} YouTube videos.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Error loading YouTube videos.");
        }
        finally { _btnYtLoad.Enabled = true; }
    }

    private async void OnYtEnableUpload(object? sender, EventArgs e)
    {
        _btnYtEnableUpload.Enabled = false;
        try
        {
            await _youTube.AuthenticateAsync();
            _btnUploadToYt.Enabled = true;
            SetStatus("YouTube upload enabled.");
            MessageBox.Show("YouTube upload is now enabled!", "Authenticated", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Authentication failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _btnYtEnableUpload.Enabled = true; }
    }

    // ================================================================== //
    // LBRY handlers
    // ================================================================== //

    private async void OnLbryCheck(object? sender, EventArgs e)
    {
        bool running = await _lbry.IsRunningAsync();
        if (!running)
        {
            MessageBox.Show("LBRY daemon not running. Open the LBRY Desktop app first.",
                "Not running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _lblLbryStatus.Text = "Not running — open LBRY Desktop";
            _lblLbryStatus.ForeColor = Color.Red;
            return;
        }
        var channels = await _lbry.GetChannelsAsync();
        if (channels.Count == 0) { MessageBox.Show("No LBRY channels found.", "No channels", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        string chosenId, chosenName;
        if (channels.Count == 1) { (chosenId, chosenName) = channels[0]; }
        else
        {
            string? picked = PickFromList("Select LBRY channel", "Choose:", channels.Select(c => c.Name).ToArray());
            if (picked == null) return;
            (chosenId, chosenName) = channels.First(c => c.Name == picked);
        }
        _lbry.SetChannel(chosenId, chosenName);
        _lblLbryStatus.Text = $"{chosenName}  (not loaded)";
        _lblLbryStatus.ForeColor = Color.DarkBlue;
        _btnLbryLoad.Enabled = true;
        SetStatus("LBRY connected.");
    }

    private async void OnLbryLoad(object? sender, EventArgs e)
    {
        _btnLbryLoad.Enabled = false;
        _lbryVideos.Clear();
        SetStatus("Loading LBRY videos...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            _lbryVideos = await _lbry.GetChannelVideosAsync(progress);
            _lblLbryStatus.Text = $"{_lbry.ChannelName} — {_lbryVideos.Count} videos loaded";
            _lblLbryStatus.ForeColor = Color.DarkGreen;
            SetStatus($"Loaded {_lbryVideos.Count} LBRY videos.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _btnLbryLoad.Enabled = true; }
    }

    // ================================================================== //
    // Compare handlers
    // ================================================================== //

    private void OnShowMissingLbry(object? sender, EventArgs e)
    {
        if (!BothLoaded(_ytVideos, _lbryVideos, "YouTube", "LBRY")) return;
        var lbryTitles = _lbryVideos.Select(v => v.Title.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _missingFromLbry = _ytVideos.Where(v => !lbryTitles.Contains(v.Title.Trim())).ToList();
        RefreshList(_lbMissingLbry, _missingFromLbry);
        _lblMissingLbryCount.Text = $"{_missingFromLbry.Count} video(s) on YouTube but not on LBRY";
        SetStatus($"Found {_missingFromLbry.Count} missing from LBRY.");
    }

    private void OnShowMissingYt(object? sender, EventArgs e)
    {
        if (!BothLoaded(_ytVideos, _lbryVideos, "YouTube", "LBRY")) return;
        var ytTitles = _ytVideos.Select(v => v.Title.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _missingFromYt = _lbryVideos.Where(v => !ytTitles.Contains(v.Title.Trim())).ToList();
        RefreshList(_lbMissingYt, _missingFromYt);
        _lblMissingYtCount.Text = $"{_missingFromYt.Count} video(s) on LBRY but not on YouTube";
        SetStatus($"Found {_missingFromYt.Count} missing from YouTube.");
    }

    // ================================================================== //
    // Download handlers
    // ================================================================== //

    private async void OnDownloadForLbry(object? sender, EventArgs e) =>
        await DownloadFromYouTube(GetSelected(_lbMissingLbry, _missingFromLbry));

    private async void OnDownloadForYt(object? sender, EventArgs e)
    {
        var video = GetSelected(_lbMissingYt, _missingFromYt);
        if (video == null) return;
        string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videos");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            string? path = await _lbry.DownloadVideoAsync(video, outputFolder, progress);
            MessageBox.Show(path != null ? $"Downloaded to:\n{path}" : "Download failed.",
                path != null ? "Done" : "Failed", MessageBoxButtons.OK,
                path != null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetStatus("Ready."); }
    }

    private async Task DownloadFromYouTube(VideoItem? video)
    {
        if (video == null) return;
        string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videos");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            string? path = await _youTube.DownloadVideoAsync(video, outputFolder, progress);
            MessageBox.Show(path != null ? $"Downloaded to:\n{path}" : "Download failed. Is yt-dlp on your PATH?",
                path != null ? "Done" : "Failed", MessageBoxButtons.OK,
                path != null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetStatus("Ready."); }
    }

    // ================================================================== //
    // Upload handlers
    // ================================================================== //

    private async void OnUploadToLbry(object? sender, EventArgs e)
    {
        var video = GetSelected(_lbMissingLbry, _missingFromLbry);
        if (video == null || !CheckDownloaded(video)) return;
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            bool ok = await _lbry.UploadVideoAsync(video, video.LocalFilePath, video.LocalThumbnailPath, _settings.LbryDefaultBid, progress);
            if (ok)
            {
                _missingFromLbry.Remove(video); _lbryVideos.Add(video);
                RefreshList(_lbMissingLbry, _missingFromLbry);
                _lblMissingLbryCount.Text = $"{_missingFromLbry.Count} video(s) on YouTube but not on LBRY";
                MessageBox.Show($"Uploaded: {video.Title}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else MessageBox.Show("Upload failed or timed out.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetStatus("Ready."); }
    }

    private async void OnUploadToYt(object? sender, EventArgs e)
    {
        var video = GetSelected(_lbMissingYt, _missingFromYt);
        if (video == null || !CheckDownloaded(video)) return;
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            bool ok = await _youTube.UploadVideoAsync(video, video.LocalFilePath, video.LocalThumbnailPath, progress);
            if (ok)
            {
                _missingFromYt.Remove(video); _ytVideos.Add(video);
                RefreshList(_lbMissingYt, _missingFromYt);
                _lblMissingYtCount.Text = $"{_missingFromYt.Count} video(s) on LBRY but not on YouTube";
                MessageBox.Show($"Uploaded: {video.Title}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else MessageBox.Show("Upload failed.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetStatus("Ready."); }
    }

    // ================================================================== //
    // Helpers
    // ================================================================== //

    private bool BothLoaded(List<VideoItem> a, List<VideoItem> b, string nameA, string nameB)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            MessageBox.Show($"Please load both {nameA} and {nameB} videos first.", "Not ready",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private bool CheckDownloaded(VideoItem video)
    {
        if (!video.IsDownloaded)
        {
            MessageBox.Show("Please download this video first.", "Not downloaded",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private VideoItem? GetSelected(ListBox lb, List<VideoItem> list)
    {
        if (lb.SelectedIndex < 0 || lb.SelectedIndex >= list.Count)
        {
            MessageBox.Show("Please select a video first.", "No selection",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        return list[lb.SelectedIndex];
    }

    private void RemoveSelected(ListBox lb, List<VideoItem> list, Label countLabel)
    {
        if (lb.SelectedIndex < 0) return;
        list.RemoveAt(lb.SelectedIndex);
        RefreshList(lb, list);
        countLabel.Text = $"{list.Count} video(s)";
    }

    private static void RefreshList(ListBox lb, List<VideoItem> items)
    {
        lb.Items.Clear();
        lb.Items.AddRange(items.Select(v => v.Title).Cast<object>().ToArray());
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired) Invoke(() => _statusLabel.Text = message);
        else _statusLabel.Text = message;
    }

    private static string? PickFromList(string title, string prompt, string[] options)
    {
        using var form = new Form
        {
            Text = title, Size = new Size(400, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        var lbl   = new Label   { Text = prompt, Left = 12, Top = 12, Width = 360 };
        var combo = new ComboBox { Left = 12, Top = 36, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(options); combo.SelectedIndex = 0;
        var btnOk     = new Button { Text = "OK",     Left = 200, Top = 110, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel",  Left = 290, Top = 110, Width = 80, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, combo, btnOk, btnCancel });
        form.AcceptButton = btnOk; form.CancelButton = btnCancel;
        return form.ShowDialog() == DialogResult.OK ? combo.SelectedItem?.ToString() : null;
    }
}
