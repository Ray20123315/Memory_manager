namespace Ray.MemoryManager.Models;
public sealed record MemorySample(DateTimeOffset Timestamp, ulong TotalPhysical, ulong AvailablePhysical, ulong CommitTotal, ulong CommitLimit)
{
    public double PhysicalUsedPercent => TotalPhysical == 0 ? 0 : 100.0 * (TotalPhysical - AvailablePhysical) / TotalPhysical;
    public double CommitUsedPercent => CommitLimit == 0 ? 0 : 100.0 * CommitTotal / CommitLimit;
    public ulong CommitHeadroom => CommitLimit > CommitTotal ? CommitLimit - CommitTotal : 0;
}
