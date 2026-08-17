using System.Diagnostics;
using System.Runtime.InteropServices;
using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed class ProcessService
{
    readonly Dictionary<int,long> _lastCommit = new();
    readonly Dictionary<int,Queue<long>> _history = new();
    static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System","Idle","Registry","Memory Compression","csrss","wininit","winlogon","lsass","services","svchost","dwm","explorer","fontdrvhost","smss","sihost","ctfmon","SecurityHealthService"
    };

    public IReadOnlyList<ProcessMemoryRow> Snapshot(int top=30)
    {
        var rows = new List<ProcessMemoryRow>();
        var live = new HashSet<int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                live.Add(p.Id);
                var name = p.ProcessName;
                var ws = p.WorkingSet64;
                var priv = p.PrivateMemorySize64;
                var commit = TryGetCommitCharge(p);
                long delta = 0;
                if (commit > 0 && _lastCommit.TryGetValue(p.Id, out var old)) delta = commit - old;
                if (commit > 0) _lastCommit[p.Id] = commit;

                if (!_history.TryGetValue(p.Id, out var q)) _history[p.Id] = q = new Queue<long>();
                if (commit > 0) { q.Enqueue(commit); while (q.Count > 8) q.Dequeue(); }

                var protectedProcess = IsProtectedName(name) || p.Id <= 4;
                var risk = protectedProcess ? "受保護" : DescribeRisk(q, commit, delta);
                rows.Add(new(p.Id,name,ws,priv,commit,delta,risk,protectedProcess));
            }
            catch { }
            finally { p.Dispose(); }
        }

        foreach (var pid in _lastCommit.Keys.Where(x => !live.Contains(x)).ToList()) { _lastCommit.Remove(pid); _history.Remove(pid); }
        return rows.OrderByDescending(x => x.CommitBytes > 0 ? x.CommitBytes : x.PrivateBytes).Take(top).ToList();
    }

    public string SafeCloseAdvice(IReadOnlyList<ProcessMemoryRow> rows)
    {
        var r = rows.FirstOrDefault(x => !x.Protected && (x.CommitBytes > 500L*1024*1024 || x.PrivateBytes > 500L*1024*1024));
        return r is null
            ? "目前沒有明顯需要先關閉的程式。"
            : $"如果真的需要釋放記憶體，可以先正常關閉 {r.Name}（Commit 約 {(r.CommitBytes > 0 ? ProcessMemoryRow.ByteText(r.CommitBytes) : r.PrivateText)}），不要直接強制結束系統程式。";
    }

    public bool IsProtectedName(string name) => ProtectedNames.Contains(name);

    static string DescribeRisk(Queue<long> history, long commit, long delta)
    {
        if (commit <= 0) return "Commit 無法讀取";
        if (history.Count >= 5)
        {
            var a = history.ToArray();
            var growth = a[^1] - a[0];
            var increasing = 0;
            for (var i=1;i<a.Length;i++) if (a[i] >= a[i-1]) increasing++;
            if (growth > 256L*1024*1024 && increasing >= a.Length-2) return "可能持續增長";
        }
        if (delta > 256L*1024*1024) return "剛剛快速增加";
        if (commit > 2L*1024*1024*1024) return "大型程式";
        return "一般";
    }

    static long TryGetCommitCharge(Process p)
    {
        try
        {
            var c = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>() };
            if (!GetProcessMemoryInfo(p.Handle, out c, c.cb)) return 0;
            var value = c.PrivateUsage.ToUInt64();
            return value > long.MaxValue ? long.MaxValue : (long)value;
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
    }

    [DllImport("psapi.dll", SetLastError=true)]
    static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint cb);
}
