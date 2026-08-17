using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Ray.MemoryManager.Services;

public sealed record MemoryPriorityChange(int Pid, uint OriginalPriority, uint TargetPriority, bool Applied, int Win32Error, string Message);

public sealed class ProcessMemoryPriorityService
{
    const uint PROCESS_SET_INFORMATION = 0x0200;
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    const int ProcessMemoryPriority = 0;
    readonly object _gate = new();
    readonly Dictionary<int,uint> _original = new();

    public bool TryGet(int pid, out uint priority, out int win32Error)
    {
        priority = 0;
        win32Error = 0;
        using var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle.IsInvalid) { win32Error = Marshal.GetLastWin32Error(); return false; }
        var info = new MEMORY_PRIORITY_INFORMATION();
        if (!GetProcessInformation(handle, ProcessMemoryPriority, ref info, (uint)Marshal.SizeOf<MEMORY_PRIORITY_INFORMATION>()))
        {
            win32Error = Marshal.GetLastWin32Error();
            return false;
        }
        priority = info.MemoryPriority;
        return true;
    }

    public MemoryPriorityChange ApplyTemporary(int pid, uint targetPriority)
    {
        targetPriority = Math.Clamp(targetPriority, 1u, 4u);
        if (!TryGet(pid, out var before, out var readError))
            return new(pid, 0, targetPriority, false, readError, "無法讀取目前 Memory Priority；不做修改。");

        lock (_gate)
        {
            if (!_original.ContainsKey(pid)) _original[pid] = before;
        }

        if (before == targetPriority)
            return new(pid, before, targetPriority, true, 0, "已經是目標 Memory Priority。");

        using var handle = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            RemoveOriginalIfNeverChanged(pid, before);
            return new(pid, before, targetPriority, false, error, "Windows 不允許修改這個 Process；保持原狀。");
        }

        var info = new MEMORY_PRIORITY_INFORMATION { MemoryPriority = targetPriority };
        if (!SetProcessInformation(handle, ProcessMemoryPriority, ref info, (uint)Marshal.SizeOf<MEMORY_PRIORITY_INFORMATION>()))
        {
            var error = Marshal.GetLastWin32Error();
            RemoveOriginalIfNeverChanged(pid, before);
            return new(pid, before, targetPriority, false, error, "設定 Memory Priority 失敗；保持原狀。");
        }

        return new(pid, before, targetPriority, true, 0, $"Memory Priority {before} → {targetPriority}");
    }

    public bool Restore(int pid, out string message)
    {
        uint original;
        lock (_gate)
        {
            if (!_original.TryGetValue(pid, out original)) { message = "沒有需要還原的設定。"; return true; }
        }

        using var handle = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 87 || error == 1168) { lock (_gate) _original.Remove(pid); message = "Process 已結束，不需要還原。"; return true; }
            message = $"無法開啟 Process 還原（Win32 {error}）。";
            return false;
        }

        var info = new MEMORY_PRIORITY_INFORMATION { MemoryPriority = original };
        if (!SetProcessInformation(handle, ProcessMemoryPriority, ref info, (uint)Marshal.SizeOf<MEMORY_PRIORITY_INFORMATION>()))
        {
            var error = Marshal.GetLastWin32Error();
            message = $"Memory Priority 還原失敗（Win32 {error}）。";
            return false;
        }

        lock (_gate) _original.Remove(pid);
        message = $"Memory Priority 已還原為 {original}。";
        return true;
    }

    public void RestoreExcept(IReadOnlySet<int> keep)
    {
        int[] ids;
        lock (_gate) ids = _original.Keys.Where(pid => !keep.Contains(pid)).ToArray();
        foreach (var pid in ids) Restore(pid, out _);
    }

    public void RestoreAll()
    {
        int[] ids;
        lock (_gate) ids = _original.Keys.ToArray();
        foreach (var pid in ids) Restore(pid, out _);
    }

    public IReadOnlyDictionary<int,uint> AppliedOriginals
    {
        get { lock (_gate) return new Dictionary<int,uint>(_original); }
    }

    void RemoveOriginalIfNeverChanged(int pid, uint before)
    {
        lock (_gate)
        {
            if (_original.TryGetValue(pid, out var original) && original == before) _original.Remove(pid);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_PRIORITY_INFORMATION { public uint MemoryPriority; }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeProcessHandle OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetProcessInformation(SafeProcessHandle hProcess, int ProcessInformationClass, ref MEMORY_PRIORITY_INFORMATION ProcessInformation, uint ProcessInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessInformation(SafeProcessHandle hProcess, int ProcessInformationClass, ref MEMORY_PRIORITY_INFORMATION ProcessInformation, uint ProcessInformationSize);
}
