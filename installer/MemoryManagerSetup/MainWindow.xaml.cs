using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace Ray.MemoryManagerSetup;

public partial class MainWindow : Window
{
    readonly InstallerService _installer = new();
    readonly string installDir = InstallerService.DefaultInstallDir;
    string AppExe => _installer.AppExe(installDir);

    public MainWindow()
    {
        InitializeComponent();
        PathText.Text = installDir;
        UninstallButton.IsEnabled = File.Exists(AppExe);
        StatusText.Text = File.Exists(AppExe) ? "已偵測到現有安裝；按「安裝 / 修復」會原子替換程式檔。" : "尚未安裝。";
    }

    async void Install_Click(object s, RoutedEventArgs e)
    {
        SetBusy(true, "正在安裝 / 修復…");
        Progress.Value = 20;
        var result = await Task.Run(() => _installer.InstallOrRepair(installDir, DesktopCheck.IsChecked == true, integrateShell: true));
        Progress.Value = result.Ok ? 100 : 0;
        StatusText.Text = result.Message;
        SetBusy(false, StatusText.Text);
        UninstallButton.IsEnabled = File.Exists(AppExe);
        if (!result.Ok) { MessageBox.Show(result.Message, "安裝失敗"); return; }
        if (MessageBox.Show("安裝 / 修復完成，要現在開啟 Memory Manager 嗎？", "完成", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo(AppExe) { UseShellExecute = true });
    }

    async void Uninstall_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("確定解除安裝 Memory Manager？Log、事故紀錄與設定備份會保留，避免誤刪診斷資料。", "解除安裝", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await Uninstall();
    }

    public async Task Uninstall(bool quiet = false)
    {
        SetBusy(true, "正在解除安裝…");
        Progress.Value = 30;
        var result = await Task.Run(() => _installer.Uninstall(installDir, integrateShell: true));
        Progress.Value = result.Ok ? 100 : 0;
        StatusText.Text = result.Message;
        SetBusy(false, StatusText.Text);
        if (!quiet && result.Ok) MessageBox.Show("解除安裝流程完成。Log、事故紀錄與設定備份依安全策略保留。", "完成");
        if (!quiet && !result.Ok) MessageBox.Show(result.Message, "失敗");
        if (result.Ok && (Environment.ProcessPath ?? string.Empty).StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
            System.Windows.Application.Current.Shutdown();
    }

    void SetBusy(bool busy, string text)
    {
        InstallButton.IsEnabled = !busy;
        UninstallButton.IsEnabled = !busy && File.Exists(AppExe);
        StatusText.Text = text;
    }
}
