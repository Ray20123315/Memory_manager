using System.IO;
using System.Text.Json;
using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed record FlightFrame(DateTimeOffset Timestamp, ulong AvailablePhysical, ulong CommitTotal, ulong CommitLimit, string ForegroundProcess);
public sealed record RecorderAction(DateTimeOffset Timestamp, string Category, string Message);

public sealed class FlightRecorderService
{
    readonly LinkedList<FlightFrame> _frames = new();
    readonly LinkedList<RecorderAction> _actions = new();
    readonly object _gate = new();
    readonly ForegroundProcessService _foreground = new();
    DateTimeOffset _lastFrameAt = DateTimeOffset.MinValue;

    public void Add(MemorySample sample)
    {
        lock (_gate)
        {
            if (sample.Timestamp - _lastFrameAt < TimeSpan.FromMilliseconds(250)) return;
            _lastFrameAt = sample.Timestamp;
            _frames.AddLast(new(sample.Timestamp, sample.AvailablePhysical, sample.CommitTotal, sample.CommitLimit, _foreground.GetForegroundProcessName()));
            while (_frames.Count > 7200) _frames.RemoveFirst();
        }
    }

    public void RecordAction(string category, string message)
    {
        lock (_gate)
        {
            _actions.AddLast(new(DateTimeOffset.Now, category, message));
            while (_actions.Count > 300) _actions.RemoveFirst();
        }
    }

    public string CommitEtaText()
    {
        lock (_gate)
        {
            if (_frames.Count < 10) return "資料累積中";
            var first = _frames.First!.Value;
            var last = _frames.Last!.Value;
            var secs = Math.Max(1, (last.Timestamp - first.Timestamp).TotalSeconds);
            var growth = (double)last.CommitTotal - first.CommitTotal;
            var headroom = last.CommitLimit > last.CommitTotal ? last.CommitLimit - last.CommitTotal : 0;
            if (growth <= 0 || headroom == 0) return headroom == 0 ? "已達上限" : "目前沒有持續上升";
            var eta = headroom / (growth / secs);
            if (double.IsInfinity(eta) || eta > 86400) return "> 24 小時";
            return eta < 60 ? $"約 {eta:0} 秒" : $"約 {eta / 60:0} 分鐘";
        }
    }

    public string ExportIncidentBundle(LogService logs, WindowsEventLogService? eventLogs = null, DateTimeOffset? center = null)
    {
        var anchor = center ?? DateTimeOffset.Now;
        var from = anchor - TimeSpan.FromMinutes(5);
        var to = anchor + TimeSpan.FromMinutes(2);
        FlightFrame[] frames;
        RecorderAction[] actions;
        lock (_gate)
        {
            frames = _frames.Where(x => x.Timestamp >= from && x.Timestamp <= to).ToArray();
            actions = _actions.Where(x => x.Timestamp >= from && x.Timestamp <= to).ToArray();
        }

        var eventResult = eventLogs?.ReadRecent(TimeSpan.FromDays(7), 400);
        var incidents = eventResult?.Events.Where(x => x.Time >= from && x.Time <= to).OrderBy(x => x.Time).ToArray() ?? [];
        var recentLogs = logs.ReadRecent(600)
            .Where(x => x.Time >= from && x.Time <= to)
            .Select(x => new { x.Time, x.Category, x.Message })
            .OrderBy(x => x.Time)
            .ToArray();

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MemoryManager-Support");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"incident-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var payload = new
        {
            created_at = DateTimeOffset.Now,
            incident_center = anchor,
            window = new { from, to },
            note = "只匯出事故前後的小時間窗，避免把不相關的長期資料整包帶走。",
            flight_frames = frames,
            manager_actions = actions,
            windows_events = incidents,
            app_logs = recentLogs,
            event_log_read_error = eventResult is { ReadSucceeded: false } ? eventResult.Error : string.Empty
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
