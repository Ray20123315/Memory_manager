using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Ray.MemoryManager.Services;

public sealed record OemControlStatus(string Manufacturer, string ProductName, bool IsMsiDevice, bool MsiCenterFound, string LaunchTarget, string Summary);

public sealed class OemControlService
{
    const string OfficialMsiCenterUrl = "https://www.msi.com/Landing/MSI-Center";

    public OemControlStatus Detect()
    {
        var manufacturer = ReadBiosValue("SystemManufacturer");
        var product = ReadBiosValue("SystemProductName");
        var isMsi = manufacturer.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || manufacturer.Contains("MSI", StringComparison.OrdinalIgnoreCase);
        var link = FindMsiCenterShortcut();
        var found = !string.IsNullOrWhiteSpace(link);
        var summary = found
            ? "已偵測到 MSI Center。硬體情境、風扇／功耗等 OEM 功能交由 MSI 官方程式控制；Memory Manager 不直接寫 EC。"
            : "未找到可啟動的 MSI Center 捷徑。可開啟 MSI 官方下載頁；Memory Manager 不使用未公開 EC 寫入介面。";
        return new(manufacturer, product, isMsi, found, found ? link! : OfficialMsiCenterUrl, summary);
    }

    public bool Launch(out string message)
    {
        var status = Detect();
        try
        {
            Process.Start(new ProcessStartInfo(status.LaunchTarget) { UseShellExecute = true });
            message = status.MsiCenterFound ? "已開啟 MSI Center。" : "未偵測到 MSI Center；已開啟 MSI 官方頁面。";
            return true;
        }
        catch (Exception ex)
        {
            message = "無法開啟 MSI Center／官方頁面：" + ex.Message;
            return false;
        }
    }

    static string ReadBiosValue(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            return key?.GetValue(name)?.ToString() ?? "未知";
        }
        catch { return "未知"; }
    }

    static string? FindMsiCenterShortcut()
    {
        var common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
        var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        var candidates = new[]
        {
            Path.Combine(common, "MSI Center.lnk"),
            Path.Combine(common, "MSI Center", "MSI Center.lnk"),
            Path.Combine(user, "MSI Center.lnk"),
            Path.Combine(user, "MSI Center", "MSI Center.lnk")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
