using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed class PreviousCrashAnalyzer
{
    public CrashAnalysis Analyze(SessionState? previous, IReadOnlyList<IncidentEvent> events)
    {
        if (previous is null)
            return new("no-baseline", "第一次建立事故基準", "目前沒有上一輪 Memory Manager 工作階段可比較。之後若發生異常，就能用 heartbeat 與 Windows 事件交叉判斷。", "未確認", null, []);

        if (previous.GracefulExit)
            return new("normal-app-exit", "上次 Memory Manager 正常結束", "上一輪有寫入正常結束標記，沒有把它誤判成系統當機。", "已確認", previous.EndedAt ?? previous.HeartbeatAt,
                [$"最後 heartbeat：{previous.HeartbeatAt:yyyy-MM-dd HH:mm:ss}", $"正常結束：{previous.EndedAt:yyyy-MM-dd HH:mm:ss}"]);

        var anchor = previous.HeartbeatAt;
        var nearby = events.Where(e => e.Time >= anchor - TimeSpan.FromMinutes(20) && e.Time <= anchor + TimeSpan.FromMinutes(90)).OrderBy(e => e.Time).ToList();
        var memory = nearby.LastOrDefault(e => e.IsMemoryPressure && e.Time <= anchor + TimeSpan.FromMinutes(2));
        var bugcheck = nearby.FirstOrDefault(e => e.IsBugCheck);
        var hardware = nearby.FirstOrDefault(e => e.IsHardwareError);
        var unexpected = nearby.FirstOrDefault(e => e.IsUnexpectedShutdown);
        var clean = nearby.FirstOrDefault(e => e.IsCleanShutdown);
        var evidence = new List<string> { $"上一輪最後 heartbeat：{anchor:yyyy-MM-dd HH:mm:ss}" };
        foreach (var e in nearby.Where(e => e.IsMemoryPressure || e.IsBugCheck || e.IsHardwareError || e.IsUnexpectedShutdown || e.IsCleanShutdown).Take(8))
            evidence.Add($"{e.Time:yyyy-MM-dd HH:mm:ss} · Event {e.EventId} · {e.Summary}");

        if (memory is not null && unexpected is not null)
            return new("memory-pressure-correlated", "當機前有明顯記憶體壓力證據", "上一輪 heartbeat 附近出現 Event 2004，之後又有非正常關機／重新啟動證據。兩者時間上高度相關，但仍不能只靠這些事件斷言唯一根因。", "高度相關", unexpected.Time, evidence);

        if (bugcheck is not null)
            return new("bugcheck-recorded", "Windows 有 BugCheck／藍畫面記錄", "上一輪未正常結束，而且附近有 BugCheck 事件。這能確認 Windows 曾記錄 bugcheck，但要看 bugcheck code 或 dump 才能判斷真正原因。", "已確認事件", bugcheck.Time, evidence);

        if (hardware is not null && unexpected is not null)
            return new("hardware-error-correlated", "非正常關機附近有 WHEA 硬體錯誤", "WHEA 與非正常關機時間相近，值得優先檢查硬體／驅動；事件本身仍不足以直接指定是哪個零件故障。", "高度相關", hardware.Time, evidence);

        if (unexpected is not null)
            return new("unexpected-shutdown-unknown", "上一輪附近有非正常關機記錄", "Windows 有 Event 41／6008 類證據，但這只能確認『沒有正常關機』，不能單獨判定是斷電、Hang、Reset、驅動或其他原因。", "已確認事件，根因未知", unexpected.Time, evidence);

        if (clean is not null)
            return new("app-exit-unclean-system-clean", "Memory Manager 上次沒有正常收尾，但 Windows 後來正常關機", "比較像 Memory Manager 被直接結束／被系統回收，而不是把它當作整台電腦當機。", "最可能判斷", clean.Time, evidence);

        return new("unclean-unknown", "上一輪沒有正常結束標記", "目前找不到足夠 Windows 事件證明是整台電腦當機。可能是 App 被強制關閉、工作階段中斷，或事件記錄不足。", "未確認", anchor, evidence);
    }

    public IReadOnlyList<string> BuildReliabilityHistory(IReadOnlyList<IncidentEvent> events, int max = 80)
    {
        return events.OrderByDescending(e => e.Time).Take(max).Select(e =>
            $"{e.Time:MM/dd HH:mm:ss}  [{FriendlyCategory(e.Category)}]  Event {e.EventId} · {e.Summary}").ToList();
    }

    public static string FriendlyCategory(string category) => category switch
    {
        "memory-pressure" => "記憶體壓力",
        "unexpected-restart" => "非正常重新啟動",
        "unexpected-shutdown" => "非正常關機",
        "clean-shutdown" => "正常關機證據",
        "eventlog-start" => "系統工作階段開始附近",
        "bugcheck" => "藍畫面 / BugCheck",
        "hardware-error" => "WHEA 硬體錯誤",
        _ => "系統事件"
    };
}
