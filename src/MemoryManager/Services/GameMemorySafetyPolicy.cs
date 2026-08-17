namespace Ray.MemoryManager.Services;

public sealed record ProtectionDecision(bool Protected, string Reason);

public sealed class GameMemorySafetyPolicy
{
    static readonly string[] AntiCheatTokens = { "easyanticheat", "beservice", "battleye", "vgc", "vgtray", "faceit", "eac" };
    static readonly string[] VoiceTokens = { "discord", "teamspeak", "mumble", "voicemod", "steelseries", "sonar" };
    static readonly string[] AccessibilityTokens = { "narrator", "magnify", "osk", "tabtip", "textinputhost" };
    static readonly string[] SecurityTokens = { "msmpeng", "nissrv", "securityhealth", "sense" };
    readonly ProcessService _processes;

    public GameMemorySafetyPolicy(ProcessService processes) => _processes = processes;

    public ProtectionDecision Classify(int pid, string processName, int foregroundPid, IReadOnlySet<string> activeGames)
    {
        if (pid == Environment.ProcessId) return new(true, "Memory Manager 自己不套規則");
        if (pid == foregroundPid && pid > 0) return new(true, "目前前景程式");
        if (_processes.IsProtectedName(processName)) return new(true, "Windows／Shell 核心程序");
        if (activeGames.Contains(processName)) return new(true, "遊戲 Profile 正在執行");
        var n = processName.ToLowerInvariant();
        if (AntiCheatTokens.Any(n.Contains)) return new(true, "反作弊相關程序");
        if (VoiceTokens.Any(n.Contains)) return new(true, "語音／通訊相關程序");
        if (AccessibilityTokens.Any(n.Contains)) return new(true, "無障礙／輸入相關程序");
        if (SecurityTokens.Any(n.Contains)) return new(true, "Windows 安全性相關程序");
        return new(false, string.Empty);
    }
}
