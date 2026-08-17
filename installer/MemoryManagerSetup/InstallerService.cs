using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;

namespace Ray.MemoryManagerSetup;

public sealed record InstallerOperationResult(bool Ok, string Message, string InstallDir);

public sealed class InstallerService
{
    public static string DefaultInstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ray20123315", "Memory Manager");
    public string AppExe(string dir) => Path.Combine(dir, "MemoryManager.exe");
    public string Uninstaller(string dir) => Path.Combine(dir, "MemoryManagerSetup.exe");

    public InstallerOperationResult InstallOrRepair(string installDir, bool desktopShortcut, bool integrateShell)
    {
        try
        {
            Directory.CreateDirectory(installDir);
            var app = AppExe(installDir);
            var temp = app + ".new";
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("MemoryManager.exe") ?? throw new InvalidOperationException("安裝包內沒有 MemoryManager.exe"))
            using (var output = File.Create(temp)) input.CopyTo(output);
            if (new FileInfo(temp).Length < 1024 * 1024) throw new InvalidOperationException("Payload 大小異常，拒絕覆蓋正式程式。");
            File.Move(temp, app, true);

            var setup = Uninstaller(installDir);
            if (!string.Equals(Environment.ProcessPath, setup, StringComparison.OrdinalIgnoreCase)) File.Copy(Environment.ProcessPath!, setup, true);

            if (integrateShell)
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Memory Manager.lnk"), app);
                var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Memory Manager.lnk");
                if (desktopShortcut) CreateShortcut(desktop, app); else TryDelete(desktop);
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RayMemoryManager");
                key.SetValue("DisplayName", "Memory Manager / 記憶體管理器");
                key.SetValue("DisplayVersion", "0.9.0-beta.29");
                key.SetValue("Publisher", "Ray20123315");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", app);
                key.SetValue("UninstallString", $"\"{setup}\" --uninstall");
                key.SetValue("QuietUninstallString", $"\"{setup}\" --uninstall --quiet");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            }

            File.WriteAllText(Path.Combine(installDir, "install-state.txt"), $"version=0.9.0-beta.29{Environment.NewLine}installed_at={DateTimeOffset.Now:O}{Environment.NewLine}");
            return new(true, "安裝 / 修復完成。", installDir);
        }
        catch (Exception ex)
        {
            return new(false, "安裝 / 修復失敗：" + ex.Message, installDir);
        }
    }

    public InstallerOperationResult Uninstall(string installDir, bool integrateShell)
    {
        try
        {
            if (integrateShell)
            {
                TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Memory Manager.lnk"));
                TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Memory Manager.lnk"));
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\RayMemoryManager", false);
            }

            var self = Environment.ProcessPath ?? string.Empty;
            if (self.StartsWith(installDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c timeout /t 2 /nobreak >nul & rmdir /s /q \"{installDir}\"") { CreateNoWindow = true, UseShellExecute = false });
                return new(true, "解除安裝已排程完成；安裝目錄會在本程式結束後刪除。", installDir);
            }

            if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
            return new(!Directory.Exists(installDir), Directory.Exists(installDir) ? "解除安裝後目錄仍存在。" : "解除安裝完成。", installDir);
        }
        catch (Exception ex)
        {
            return new(false, "解除安裝失敗：" + ex.Message, installDir);
        }
    }

    public bool AuditInstalled(string installDir, out string message)
    {
        var app = AppExe(installDir);
        var setup = Uninstaller(installDir);
        var state = Path.Combine(installDir, "install-state.txt");
        var ok = File.Exists(app) && File.Exists(setup) && File.Exists(state) && new FileInfo(app).Length > 1024 * 1024;
        message = ok ? "安裝佈局完整。" : "安裝佈局不完整。";
        return ok;
    }

    static void CreateShortcut(string path, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("無法使用 Windows Shortcut API");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = target;
        link.WorkingDirectory = Path.GetDirectoryName(target);
        link.IconLocation = target + ",0";
        link.Save();
    }

    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
