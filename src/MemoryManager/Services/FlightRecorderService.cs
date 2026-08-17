using System.IO;
using System.Text.Json;
using Ray.MemoryManager.Models;
namespace Ray.MemoryManager.Services;
public sealed class FlightRecorderService
{
    readonly LinkedList<MemorySample> _samples=new(); readonly object _gate=new();
    public void Add(MemorySample s) { lock(_gate){ _samples.AddLast(s); while(_samples.Count>6000) _samples.RemoveFirst(); } }
    public string CommitEtaText()
    {
        lock(_gate) { if(_samples.Count<10) return "資料累積中"; var first=_samples.First!.Value; var last=_samples.Last!.Value; var secs=Math.Max(1,(last.Timestamp-first.Timestamp).TotalSeconds); var growth=(double)last.CommitTotal-first.CommitTotal; if(growth<=0 || last.CommitHeadroom==0) return last.CommitHeadroom==0?"已達上限":"目前沒有持續上升"; var rate=growth/secs; var eta=last.CommitHeadroom/rate; if(double.IsInfinity(eta)||eta>86400) return "> 24 小時"; return eta<60? $"約 {eta:0} 秒" : $"約 {eta/60:0} 分鐘"; }
    }
    public string ExportIncidentBundle(LogService logs)
    {
        var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"MemoryManager-Support"); Directory.CreateDirectory(dir); var path=Path.Combine(dir,$"incident-{DateTime.Now:yyyyMMdd-HHmmss}.json"); MemorySample[] samples; lock(_gate) samples=_samples.ToArray(); File.WriteAllText(path,JsonSerializer.Serialize(new {created_at=DateTimeOffset.Now,samples,log_file=logs.CurrentLogPath},new JsonSerializerOptions{WriteIndented=true})); return path;
    }
}
