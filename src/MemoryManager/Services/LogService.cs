using System.Diagnostics;
using System.IO;
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
        return File.ReadLines(CurrentLogPath).Reverse().Take(max).Select(line => {
            var p=line.Split('\t',3);
            var rawTime = p.Length > 0 ? p[0] : string.Empty;
            var category = p.Length > 1 ? p[1] : "App";
            var message = p.Length > 2 ? p[2] : line;
            return (DateTimeOffset.TryParse(rawTime,out var t)?t:DateTimeOffset.Now,category,message);
        }).ToList();
    }
}
