using System.IO;
using System.IO.Compression;

namespace Ray.MemoryManager.Services;

public sealed class SettingsBackupService
{
    readonly string _settingsDir;
    readonly string _backupDir;

    public SettingsBackupService(string? settingsDir = null, string? backupDir = null)
    {
        _settingsDir = settingsDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ray", "MemoryManager", "settings");
        _backupDir = backupDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ray", "MemoryManager", "backups");
        Directory.CreateDirectory(_settingsDir);
        Directory.CreateDirectory(_backupDir);
    }

    public string CreateBackup()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var unique = Guid.NewGuid().ToString("N")[..8];
        var path = Path.Combine(_backupDir, $"settings-{stamp}-{unique}.zip");
        ZipFile.CreateFromDirectory(_settingsDir, path, CompressionLevel.Fastest, includeBaseDirectory: false);
        return path;
    }

    public IReadOnlyList<string> ListBackups() => Directory.Exists(_backupDir)
        ? Directory.EnumerateFiles(_backupDir, "settings-*.zip").OrderByDescending(File.GetLastWriteTimeUtc).ToList()
        : [];

    public bool RestoreLatest(out string message)
    {
        var latest = ListBackups().FirstOrDefault();
        if (latest is null)
        {
            message = "沒有可還原的設定備份。";
            return false;
        }

        try
        {
            var safety = CreateBackup();
            var temp = Path.Combine(Path.GetTempPath(), "memory-manager-settings-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                ZipFile.ExtractToDirectory(latest, temp);
                foreach (var file in Directory.EnumerateFiles(_settingsDir)) File.Delete(file);
                foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(temp, file);
                    var target = Path.Combine(_settingsDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, true);
                }
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
            message = $"已還原 {Path.GetFileName(latest)}；還原前安全備份：{Path.GetFileName(safety)}。重新啟動程式後完整生效。";
            return true;
        }
        catch (Exception ex)
        {
            message = "設定還原失敗：" + ex.Message;
            return false;
        }
    }

    public string BackupDirectory => _backupDir;
}
