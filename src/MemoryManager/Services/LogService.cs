using System.Diagnostics;
using System.Text;
namespace Ray.MemoryManager.Services;
public sealed class LogService
{
    public string LogDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ray", "MemoryManager", "logs");
    public string CurrentLogPath => Path.Combine(LogDirectory, $"memory-manager-{DateTime.Now:yyyy-MM-dd}.log");
    public LogService() => Directory.CreateDirectory(LogDirectory);
    public void Write(string category, string message) { Directory.CreateDirectory(LogDirectory); File.AppendAllText(CurrentLogPath, $"{DateTimeOffset.Now:O}\t{category}\t{message}{Environment.NewLine}", Encoding.UTF8); }
    public void OpenDirectory() { Directory.CreateDirectory(LogDirectory); Process.Start(new ProcessStartInfo { FileName = LogDirectory, UseShellExecute = true }); }
    public IReadOnlyList<(DateTimeOffset Time,string Category,string Message)> ReadRecent(int max=200)
    {
        if (!File.Exists(CurrentLogPath)) return [];
        return File.ReadLines(CurrentLogPath).Reverse().Take(max).Select(line => { var p=line.Split('\t',3); return (DateTimeOffset.TryParse(p.ElementAtOrDefault(0),out var t)?t:DateTimeOffset.Now,p.ElementAtOrDefault(1)??"App",p.ElementAtOrDefault(2)??line); }).ToList();
    }
}
