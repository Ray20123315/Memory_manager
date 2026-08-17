namespace Ray.MemoryManager.Models;

public sealed record ProcessMemoryRow(int Pid, string Name, long WorkingSet, long PrivateBytes, long CommitBytes, long DeltaBytes, string Risk, bool Protected)
{
    public string WorkingSetText => ByteText(WorkingSet);
    public string PrivateText => ByteText(PrivateBytes);
    public string CommitText => ByteText(CommitBytes);
    public string DeltaText => (DeltaBytes >= 0 ? "+" : "") + ByteText(DeltaBytes);

    public static string ByteText(long value)
    {
        var abs = Math.Abs((double)value);
        if (abs >= 1024 * 1024 * 1024) return $"{value / 1024d / 1024 / 1024:0.0} GB";
        return $"{value / 1024d / 1024:0} MB";
    }
}
