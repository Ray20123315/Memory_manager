using System.Diagnostics;
using Ray.MemoryManager.Models;
namespace Ray.MemoryManager.Services;
public sealed class ProcessService
{
    readonly Dictionary<int,(long bytes,DateTimeOffset at)> _last = new();
    static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase) {"System","Idle","Registry","Memory Compression","csrss","wininit","winlogon","lsass","services","svchost","dwm","explorer"};
    public IReadOnlyList<ProcessMemoryRow> Snapshot(int top=30)
    {
        var now=DateTimeOffset.Now; var rows=new List<ProcessMemoryRow>();
        foreach(var p in Process.GetProcesses()) { try { var name=p.ProcessName; var ws=p.WorkingSet64; var priv=p.PrivateMemorySize64; long delta=0; if(_last.TryGetValue(p.Id,out var old)) delta=priv-old.bytes; _last[p.Id]=(priv,now); var risk=Protected.Contains(name)?"受保護":(delta>256L*1024*1024?"可能快速增長":priv>2L*1024*1024*1024?"大型程式":"一般"); rows.Add(new(p.Id,name,ws,priv,delta,risk)); } catch { } finally { p.Dispose(); } }
        return rows.OrderByDescending(x=>x.PrivateBytes).Take(top).ToList();
    }
    public string SafeCloseAdvice(IReadOnlyList<ProcessMemoryRow> rows) { var r=rows.FirstOrDefault(x=>x.Risk!="受保護" && x.PrivateBytes>500L*1024*1024); return r is null ? "目前沒有明顯需要先關閉的程式。" : $"如果真的需要釋放記憶體，可以先正常關閉 {r.Name}（約 {r.PrivateText}），不要直接強制結束系統程式。"; }
}
