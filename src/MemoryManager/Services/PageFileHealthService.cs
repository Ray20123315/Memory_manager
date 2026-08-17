using Microsoft.Win32;

namespace Ray.MemoryManager.Services;

public sealed record PageFileHealth(bool RegistryReadable, bool Configured, bool SystemManaged, IReadOnlyList<string> Entries, string Summary);

public sealed class PageFileHealthService
{
    const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

    public PageFileHealth Read()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: false);
            var raw = key?.GetValue("PagingFiles") as string[] ?? [];
            var entries = raw.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (entries.Count == 0)
                return new(true, false, false, entries, "沒有讀到 Page File 設定；Windows 仍可能使用自動管理，建議到系統設定確認。");

            var systemManaged = entries.Any(IsSystemManagedEntry);
            var summary = systemManaged
                ? "Page File：Windows 自動管理（建議一般使用者維持這個設定）"
                : "Page File：使用自訂大小；如果常遇到 Commit 不足，可再檢查容量。";
            return new(true, true, systemManaged, entries, summary);
        }
        catch (Exception ex)
        {
            return new(false, false, false, [], "目前無法讀取 Page File 設定：" + ex.Message);
        }
    }

    internal static bool IsSystemManagedEntry(string entry)
    {
        var p = entry.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return p.Length >= 3 && p[^2] == "0" && p[^1] == "0";
    }
}
