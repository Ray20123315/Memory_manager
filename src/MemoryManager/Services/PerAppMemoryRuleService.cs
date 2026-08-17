using System.IO;
using System.Text.Json;

namespace Ray.MemoryManager.Services;

public sealed record PerAppMemoryRule(
    string Id,
    string ProcessName,
    bool Enabled,
    int IdleMinutes,
    long MinCommitMb,
    uint TargetMemoryPriority,
    DateTimeOffset CreatedAt)
{
    public override string ToString() => $"{ProcessName} · 背景 {IdleMinutes} 分鐘 + Commit ≥ {MinCommitMb} MB → Memory Priority {TargetMemoryPriority} · {(Enabled ? "規則啟用" : "規則停用")}";
}

public sealed class PerAppMemoryRuleService
{
    readonly string _path;
    readonly List<PerAppMemoryRule> _rules;
    public IReadOnlyList<PerAppMemoryRule> Rules => _rules;

    public PerAppMemoryRuleService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ray", "MemoryManager", "settings", "per-app-memory-rules.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _rules = Load(_path);
    }

    public PerAppMemoryRule AddOrEnable(string processName, int idleMinutes = 15, long minCommitMb = 2048, uint targetMemoryPriority = 2)
    {
        if (string.IsNullOrWhiteSpace(processName)) throw new ArgumentException("Process name is required.", nameof(processName));
        targetMemoryPriority = Math.Clamp(targetMemoryPriority, 1u, 4u);
        var index = _rules.FindIndex(x => string.Equals(x.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _rules[index] = _rules[index] with { Enabled = true, IdleMinutes = Math.Max(1, idleMinutes), MinCommitMb = Math.Max(64, minCommitMb), TargetMemoryPriority = targetMemoryPriority };
            Save();
            return _rules[index];
        }

        var rule = new PerAppMemoryRule(Guid.NewGuid().ToString("N"), processName, true, Math.Max(1, idleMinutes), Math.Max(64, minCommitMb), targetMemoryPriority, DateTimeOffset.Now);
        _rules.Add(rule);
        Save();
        return rule;
    }

    public void SetEnabled(string id, bool enabled)
    {
        var index = _rules.FindIndex(x => x.Id == id);
        if (index < 0) return;
        _rules[index] = _rules[index] with { Enabled = enabled };
        Save();
    }

    public void Remove(string id)
    {
        _rules.RemoveAll(x => x.Id == id);
        Save();
    }

    void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _path, true);
    }

    static List<PerAppMemoryRule> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<PerAppMemoryRule>>(File.ReadAllText(path)) ?? [];
        }
        catch { return []; }
    }
}
