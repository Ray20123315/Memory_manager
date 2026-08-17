using System.Diagnostics.Eventing.Reader;
using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed class WindowsEventLogService
{
    public EventLogReadResult ReadRecent(TimeSpan window, int maxEvents = 300)
    {
        var ms = Math.Clamp((long)window.TotalMilliseconds, 1, 2_147_000_000L);
        var xpath = $"*[System[((EventID=41 or EventID=6008 or EventID=6006 or EventID=6005 or EventID=2004 or EventID=1001) or Provider[@Name='Microsoft-Windows-WHEA-Logger']) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";

        try
        {
            var query = new EventLogQuery("System", PathType.LogName, xpath)
            {
                ReverseDirection = true,
                TolerateQueryErrors = true
            };
            using var reader = new EventLogReader(query);
            var events = new List<IncidentEvent>();
            for (var i = 0; i < maxEvents; i++)
            {
                using var record = reader.ReadEvent();
                if (record is null) break;
                var normalized = Normalize(record);
                if (normalized is not null) events.Add(normalized);
            }
            return new(true, string.Empty, events);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message, []);
        }
    }

    internal static IncidentEvent? Normalize(EventRecord record)
    {
        var provider = record.ProviderName ?? "Unknown";
        var id = record.Id;
        var category = Classify(provider, id);
        if (category is null) return null;
        var time = record.TimeCreated.HasValue
            ? new DateTimeOffset(record.TimeCreated.Value)
            : DateTimeOffset.Now;
        var detail = SafeDescription(record);
        return new(time, provider, id, category, Summary(category), record.LevelDisplayName ?? string.Empty, detail);
    }

    public static string? Classify(string provider, int id)
    {
        if (provider.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase)) return "hardware-error";
        if (id == 2004 && provider.Contains("Resource-Exhaustion-Detector", StringComparison.OrdinalIgnoreCase)) return "memory-pressure";
        if (id == 41 && provider.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase)) return "unexpected-restart";
        if (id == 6008) return "unexpected-shutdown";
        if (id == 6006) return "clean-shutdown";
        if (id == 6005) return "eventlog-start";
        if (id == 1001 && (provider.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) || provider.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase))) return "bugcheck";
        return null;
    }

    public static string Summary(string category) => category switch
    {
        "memory-pressure" => "Windows 記錄到 Commit／虛擬記憶體資源不足。",
        "unexpected-restart" => "Windows 記錄：上一次沒有完成正常關機；這個事件本身不能證明根因。",
        "unexpected-shutdown" => "前一次關機被記錄為非預期。",
        "clean-shutdown" => "Windows Event Log 服務正常停止，可作為正常關機的輔助證據。",
        "eventlog-start" => "Windows Event Log 服務開始，可作為新一次系統工作階段附近的時間參考。",
        "bugcheck" => "Windows 記錄到 BugCheck／藍畫面相關事件。",
        "hardware-error" => "WHEA 記錄到硬體錯誤事件；仍需看詳細資料才能判斷硬體與嚴重度。",
        _ => "Windows 系統事件。"
    };

    static string SafeDescription(EventRecord record)
    {
        try
        {
            var text = record.FormatDescription() ?? string.Empty;
            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 1000 ? text : text[..1000] + "…";
        }
        catch
        {
            return string.Empty;
        }
    }
}
