using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using Ray.MemoryManager.Services;

namespace Ray.MemoryManager;

public partial class MainWindow : Window
{
    readonly MemoryTelemetryService _telemetry = new();
    readonly LogService _logs = new();
    readonly NotificationService _notifications = new();
    readonly UpdateService _updates = new();
    readonly ProcessService _processes = new();
    readonly FlightRecorderService _flight = new();
    readonly PageFileHealthService _pageFile = new();
    readonly AdaptiveRefreshService _adaptive = new();
    readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    DateTimeOffset _lastProcessRefresh = DateTimeOffset.MinValue;
    DateTimeOffset _lastPageFileRefresh = DateTimeOffset.MinValue;
    PageFileHealth? _pageFileHealth;

    public MainWindow()
    {
        InitializeComponent();
        NotificationList.ItemsSource = _notifications.Items;
        _telemetry.Sampled += s => _flight.Add(s);
        _telemetry.Start(10);
        _uiTimer.Tick += (_, _) => RefreshDashboard();
        _uiTimer.Start();
        _updateTimer.Tick += async (_, _) => { if (UpdateNotifyCheck.IsChecked == true) await CheckUpdates(true); };
        _updateTimer.Start();

        RefreshPageFileHealth();
        _logs.Write("啟動", "Memory Manager 已啟動，主視窗預設最大化。");
        _notifications.Add("beta.29 開發版", "已加入真正 Process Commit、Adaptive Refresh、Page File 狀態與 Emergency Rescue。", "feature");
        Loaded += async (_, _) =>
        {
            RefreshProcesses();
            RefreshLogs();
            RefreshDashboard();
            await CheckUpdatesQuietly();
        };
        Closed += (_, _) => { _telemetry.Dispose(); _notifications.Dispose(); };
    }

    void Nav_Click(object sender, RoutedEventArgs e) => ShowPage((sender as WpfButton)?.Tag?.ToString());

    void ShowPage(string? tag)
    {
        foreach (var g in new[] { DashboardPage, ProcessesPage, NotificationsPage, LogsPage, SettingsPage, AboutPage })
            g.Visibility = Visibility.Collapsed;

        (tag switch
        {
            "processes" => ProcessesPage,
            "notifications" => NotificationsPage,
            "logs" => LogsPage,
            "settings" => SettingsPage,
            "about" => AboutPage,
            _ => DashboardPage
        }).Visibility = Visibility.Visible;

        if (tag == "logs") RefreshLogs();
        if (tag == "processes") RefreshProcesses();
    }

    void RefreshDashboard()
    {
        var s = _telemetry.Latest;
        if (s.TotalPhysical == 0) return;

        AvailableText.Text = Bytes(s.AvailablePhysical);
        UsedText.Text = $"{s.PhysicalUsedPercent:0.0}%";
        CommitText.Text = $"{s.CommitUsedPercent:0.0}%";
        CommitBar.Value = s.CommitUsedPercent;
        CommitDetailText.Text = $"已用 {Bytes(s.CommitTotal)} / 上限 {Bytes(s.CommitLimit)}，還有 {Bytes(s.CommitHeadroom)} 緩衝";
        EtaText.Text = _flight.CommitEtaText();

        var emergency = s.CommitUsedPercent >= 95 || s.AvailablePhysical < 768UL * 1024 * 1024;
        var pressure = s.CommitUsedPercent >= 85 || s.AvailablePhysical < 2UL * 1024 * 1024 * 1024;
        if (emergency)
        {
            HealthText.Text = "危險：記憶體快用完";
            HealthText.Foreground = MediaBrushes.OrangeRed;
            SideStatus.Text = "Emergency · 建議先看救援面板";
            EmergencyActionsCard.Visibility = Visibility.Visible;
        }
        else if (pressure)
        {
            HealthText.Text = "注意：壓力偏高";
            HealthText.Foreground = MediaBrushes.Orange;
            SideStatus.Text = "Pressure high";
            EmergencyActionsCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            HealthText.Text = "正常，不需要亂清 RAM";
            HealthText.Foreground = MediaBrushes.LightGreen;
            SideStatus.Text = "Normal";
            EmergencyActionsCard.Visibility = Visibility.Collapsed;
        }

        if (AdaptiveRefreshCheck.IsChecked == true)
        {
            var visible = IsVisible && WindowState != System.Windows.WindowState.Minimized;
            var ms = _adaptive.ChooseUiIntervalMs(visible, IsActive, gameMode: false, s);
            if ((int)_uiTimer.Interval.TotalMilliseconds != ms)
                _uiTimer.Interval = TimeSpan.FromMilliseconds(ms);
            AdaptiveExplainText.Text = _adaptive.Explain(ms);
        }

        if (DateTimeOffset.Now - _lastProcessRefresh > TimeSpan.FromSeconds(3))
        {
            RefreshProcesses();
            _lastProcessRefresh = DateTimeOffset.Now;
        }
        if (DateTimeOffset.Now - _lastPageFileRefresh > TimeSpan.FromMinutes(1)) RefreshPageFileHealth();
    }

    void RefreshPageFileHealth()
    {
        _pageFileHealth = _pageFile.Read();
        PageFileText.Text = _pageFileHealth.Summary;
        _lastPageFileRefresh = DateTimeOffset.Now;
    }

    void RefreshProcesses_Click(object s, RoutedEventArgs e) => RefreshProcesses();

    void RefreshProcesses()
    {
        var rows = _processes.Snapshot();
        ProcessGrid.ItemsSource = rows;
        ProcessAdviceText.Text = _processes.SafeCloseAdvice(rows);
        AdvisorText.Text = ProcessAdviceText.Text;
    }

    void OpenProcesses_Click(object s, RoutedEventArgs e)
    {
        ShowPage("processes");
        _logs.Write("救援", "從 Emergency Rescue 開啟程式清單。");
    }

    void OpenTaskManager_Click(object s, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            _logs.Write("救援", "已開啟 Windows 工作管理員。");
        }
        catch (Exception ex)
        {
            _logs.Write("錯誤", "無法開啟工作管理員：" + ex.Message);
            _notifications.Add("無法開啟工作管理員", "請按 Ctrl + Shift + Esc 手動開啟。", "warn");
            ShowPage("notifications");
        }
    }

    void RefreshLogs_Click(object s, RoutedEventArgs e) => RefreshLogs();

    void RefreshLogs()
    {
        LogList.ItemsSource = _logs.ReadRecent()
            .Select(x => $"{x.Time:HH:mm:ss}  [{x.Category}]  {Plain(x.Category, x.Message)}")
            .ToList();
    }

    string Plain(string c, string m) => c switch
    {
        "更新" => $"更新檢查：{m}",
        "錯誤" => $"需要注意：{m}",
        "啟動" => $"程式狀態：{m}",
        "救援" => $"救援工具：{m}",
        _ => m
    };

    void OpenLogs_Click(object s, RoutedEventArgs e)
    {
        try
        {
            _logs.OpenDirectory();
            _logs.Write("Log", "已開啟實際 Log 資料夾。");
        }
        catch (Exception ex)
        {
            _logs.Write("錯誤", "無法開啟 Log 資料夾：" + ex.Message);
            _notifications.Add("Log 資料夾開啟失敗", "程式已保留錯誤內容；請到通知頁查看。", "warn");
            ShowPage("notifications");
        }
    }

    async void CheckUpdate_Click(object s, RoutedEventArgs e) => await CheckUpdates(false);
    async Task CheckUpdatesQuietly() { if (UpdateNotifyCheck.IsChecked == true) await CheckUpdates(true); }

    async Task CheckUpdates(bool quiet)
    {
        try
        {
            var r = await _updates.CheckAsync();
            if (r.HasUpdate)
            {
                _notifications.Add("有新版可用", $"{r.Tag} 已發布。可到 Releases 查看新功能與改進。", "update", true);
                _logs.Write("更新", $"發現新版 {r.Tag}");
            }
            else if (!quiet)
            {
                _notifications.Add("已是目前版本", string.IsNullOrWhiteSpace(r.Tag) ? "沒有讀到新版 Release。" : $"目前 Release：{r.Tag}");
            }
        }
        catch (Exception ex)
        {
            _logs.Write("錯誤", "更新檢查失敗：" + ex.Message);
            if (!quiet) _notifications.Add("更新檢查失敗", "目前無法連線 GitHub；不影響本機功能。", "warn");
        }
    }

    void ExportIncident_Click(object s, RoutedEventArgs e)
    {
        var p = _flight.ExportIncidentBundle(_logs);
        _notifications.Add("事故資料已匯出", $"已放到 {p}");
        _logs.Write("診斷", "已匯出 Incident Bundle：" + p);
    }

    void SampleCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var ms = ParseMs((SampleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), 10);
        _telemetry.Start(ms);
        _logs.Write("設定", $"Telemetry 改為 {ms} ms");
    }

    void UiCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || AdaptiveRefreshCheck.IsChecked == true) return;
        var txt = (UiCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "250";
        _uiTimer.Interval = TimeSpan.FromMilliseconds(ParseMs(txt, 250));
        AdaptiveExplainText.Text = "手動模式：畫面固定每 " + (int)_uiTimer.Interval.TotalMilliseconds + " ms 更新。";
    }

    void ThemeCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var light = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "淺色";
        ApplyTheme(light);
        _logs.Write("設定", "外觀改為" + (light ? "淺色" : "深色") + "。");
    }

    static void ApplyTheme(bool light)
    {
        var r = System.Windows.Application.Current.Resources;
        if (light)
        {
            r["Bg"] = Brush(0xF6, 0xF8, 0xFC);
            r["Side"] = Brush(0xFF, 0xFF, 0xFF);
            r["Panel"] = Brush(0xFF, 0xFF, 0xFF);
            r["Panel2"] = Brush(0xF0, 0xF3, 0xF8);
            r["Text"] = Brush(0x14, 0x1A, 0x26);
            r["Muted"] = Brush(0x62, 0x70, 0x84);
            r["Outline"] = Brush(0xDA, 0xE1, 0xEC);
        }
        else
        {
            r["Bg"] = Brush(0x08, 0x0B, 0x12);
            r["Side"] = Brush(0x0C, 0x11, 0x1B);
            r["Panel"] = Brush(0x10, 0x16, 0x22);
            r["Panel2"] = Brush(0x15, 0x1D, 0x2B);
            r["Text"] = Brush(0xEE, 0xF4, 0xFF);
            r["Muted"] = Brush(0x8F, 0x9B, 0xB0);
            r["Outline"] = Brush(0x26, 0x32, 0x47);
        }
    }

    static SolidColorBrush Brush(byte r, byte g, byte b) => new(System.Windows.Media.Color.FromRgb(r, g, b));
    static int ParseMs(string? t, int d) => int.TryParse(new string((t ?? "").TakeWhile(char.IsDigit).ToArray()), out var v) ? v : d;
    static string Bytes(ulong b) => b >= 1024UL * 1024 * 1024 ? $"{b / 1024d / 1024 / 1024:0.0} GB" : $"{b / 1024d / 1024:0} MB";
}
