using System.Runtime.InteropServices;
using Ray.MemoryManager.Models;
namespace Ray.MemoryManager.Services;
public sealed class MemoryTelemetryService : IDisposable
{
    readonly object _gate = new();
    CancellationTokenSource? _cts;
    public int SamplingIntervalMs { get; private set; } = 10;
    public MemorySample Latest { get; private set; } = ReadOnce();
    public event Action<MemorySample>? Sampled;
    public void Start(int intervalMs)
    {
        SamplingIntervalMs = Math.Clamp(intervalMs, 1, 5000);
        _cts?.Cancel(); _cts = new CancellationTokenSource(); var ct = _cts.Token;
        _ = Task.Run(async () => { using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(SamplingIntervalMs)); try { while (await timer.WaitForNextTickAsync(ct)) { var sample = ReadOnce(); lock (_gate) Latest = sample; Sampled?.Invoke(sample); } } catch (OperationCanceledException) { } }, ct);
    }
    public void Dispose() => _cts?.Cancel();
    public static MemorySample ReadOnce()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() }; if (!GlobalMemoryStatusEx(ref mem)) return new(DateTimeOffset.Now,0,0,0,0);
        var perf = new PERFORMANCE_INFORMATION { cb = Marshal.SizeOf<PERFORMANCE_INFORMATION>() }; GetPerformanceInfo(out perf, perf.cb); ulong page = (ulong)Math.Max(1, perf.PageSize.ToInt64());
        return new MemorySample(DateTimeOffset.Now, mem.ullTotalPhys, mem.ullAvailPhys,(ulong)Math.Max(0, perf.CommitTotal.ToInt64()) * page,(ulong)Math.Max(0, perf.CommitLimit.ToInt64()) * page);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
    [StructLayout(LayoutKind.Sequential)] struct PERFORMANCE_INFORMATION { public int cb; public IntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize; public int HandleCount, ProcessCount, ThreadCount; }
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    [DllImport("psapi.dll", SetLastError=true)] static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, int cb);
}
