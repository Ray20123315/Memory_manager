namespace Ray.MemoryManager.Models;

public sealed record IncidentEvent(
    DateTimeOffset Time,
    string Provider,
    int EventId,
    string Category,
    string Summary,
    string Level,
    string Detail)
{
    public bool IsUnexpectedShutdown => Category is "unexpected-restart" or "unexpected-shutdown";
    public bool IsMemoryPressure => Category == "memory-pressure";
    public bool IsBugCheck => Category == "bugcheck";
    public bool IsHardwareError => Category == "hardware-error";
    public bool IsCleanShutdown => Category == "clean-shutdown";
}

public sealed record EventLogReadResult(bool ReadSucceeded, string Error, IReadOnlyList<IncidentEvent> Events);

public sealed record CrashAnalysis(
    string Code,
    string Title,
    string Summary,
    string Confidence,
    DateTimeOffset? AnchorTime,
    IReadOnlyList<string> Evidence);
