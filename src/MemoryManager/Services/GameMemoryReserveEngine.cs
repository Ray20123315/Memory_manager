using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed record MemoryRuleDecision(int Pid, string ProcessName, string State, string Reason, uint? TargetPriority)
{
    public override string ToString() => $"{ProcessName} ({Pid}) · {State} · {Reason}";
}

public sealed record GameReserveSnapshot(string Foreground, IReadOnlyList<string> ActiveGames, IReadOnlyList<MemoryRuleDecision> Decisions)
{
    public string GameSummary => ActiveGames.Count == 0 ? "目前沒有偵測到執行中的遊戲 Profile。" : "保護中的遊戲：" + string.Join(", ", ActiveGames);
}

public sealed class GameMemoryReserveEngine : IDisposable
{
    readonly ProcessService _processes;
    readonly ForegroundProcessService _foreground;
    readonly GameProfileService _profiles;
    readonly PerAppMemoryRuleService _rules;
    readonly ProcessMemoryPriorityService _priority;
    readonly GameMemorySafetyPolicy _safety;
    readonly Dictionary<int,DateTimeOffset> _firstSeen = new();
    readonly Dictionary<string,DateTimeOffset> _lastForeground = new(StringComparer.OrdinalIgnoreCase);

    public GameMemoryReserveEngine(ProcessService processes, ForegroundProcessService foreground, GameProfileService profiles, PerAppMemoryRuleService rules, ProcessMemoryPriorityService priority)
    {
        _processes = processes;
        _foreground = foreground;
        _profiles = profiles;
        _rules = rules;
        _priority = priority;
        _safety = new GameMemorySafetyPolicy(processes);
    }

    public GameReserveSnapshot Evaluate(bool masterEnabled, bool autoDetectProfiles)
    {
        var now = DateTimeOffset.Now;
        var foreground = _foreground.GetForegroundProcess();
        if (!string.IsNullOrWhiteSpace(foreground.Name)) _lastForeground[foreground.Name] = now;
        if (autoDetectProfiles) _profiles.EnsureAutoDetected(foreground);

        var rows = _processes.Snapshot(500);
        var liveIds = rows.Select(x => x.Pid).ToHashSet();
        foreach (var row in rows) if (!_firstSeen.ContainsKey(row.Pid)) _firstSeen[row.Pid] = now;
        foreach (var id in _firstSeen.Keys.Where(id => !liveIds.Contains(id)).ToList()) _firstSeen.Remove(id);

        var runningNames = rows.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeGames = _profiles.Profiles.Where(x => x.Enabled && runningNames.Contains(x.ProcessName)).Select(x => x.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var decisions = new List<MemoryRuleDecision>();

        if (!masterEnabled)
        {
            _priority.RestoreAll();
            decisions.Add(new(0, "規則總開關", "停用", "所有背景 Memory Priority 修改都已還原。", null));
            return new(foreground.Name, activeGames.OrderBy(x => x).ToList(), decisions);
        }

        var keep = new HashSet<int>();
        foreach (var row in rows)
        {
            var rule = _rules.Rules.FirstOrDefault(x => x.Enabled && string.Equals(x.ProcessName, row.Name, StringComparison.OrdinalIgnoreCase));
            if (rule is null) continue;

            var protection = _safety.Classify(row.Pid, row.Name, foreground.Pid, activeGames);
            if (protection.Protected)
            {
                decisions.Add(new(row.Pid, row.Name, "保護", protection.Reason, null));
                continue;
            }

            var lastForeground = _lastForeground.TryGetValue(row.Name, out var last) ? last : _firstSeen[row.Pid];
            var idle = now - lastForeground;
            if (idle < TimeSpan.FromMinutes(rule.IdleMinutes))
            {
                decisions.Add(new(row.Pid, row.Name, "等待", $"背景時間 {idle.TotalMinutes:0.0}/{rule.IdleMinutes} 分鐘", null));
                continue;
            }

            var commitMb = row.CommitBytes / 1024d / 1024;
            if (row.CommitBytes <= 0 || commitMb < rule.MinCommitMb)
            {
                decisions.Add(new(row.Pid, row.Name, "等待", $"Commit {commitMb:0} MB，門檻 {rule.MinCommitMb} MB", null));
                continue;
            }

            var changed = _priority.ApplyTemporary(row.Pid, rule.TargetMemoryPriority);
            if (changed.Applied)
            {
                keep.Add(row.Pid);
                decisions.Add(new(row.Pid, row.Name, "已套用", changed.Message, rule.TargetMemoryPriority));
            }
            else
            {
                decisions.Add(new(row.Pid, row.Name, "未修改", changed.Message + (changed.Win32Error != 0 ? $" Win32={changed.Win32Error}" : string.Empty), null));
            }
        }

        _priority.RestoreExcept(keep);
        return new(foreground.Name, activeGames.OrderBy(x => x).ToList(), decisions);
    }

    public void Dispose() => _priority.RestoreAll();
}
