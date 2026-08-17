using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Ray.MemoryManager.Services;

public static class SelfTestService
{
    public static int Run(string outputPath)
    {
        var checks = new List<object>();
        var ok = true;
        try
        {
            var sample = MemoryTelemetryService.ReadOnce();
            var memoryOk = sample.TotalPhysical > 0 && sample.CommitLimit > 0;
            checks.Add(new { name = "system-memory", ok = memoryOk, total = sample.TotalPhysical, available = sample.AvailablePhysical, commit = sample.CommitTotal, limit = sample.CommitLimit });
            ok &= memoryOk;

            var page = new PageFileHealthService().Read();
            checks.Add(new { name = "page-file-registry", ok = page.RegistryReadable, page.Configured, page.SystemManaged, page.Summary });
            ok &= page.RegistryReadable;

            var rows = new ProcessService().Snapshot(200);
            var self = rows.FirstOrDefault(x => x.Pid == Environment.ProcessId);
            var processOk = self is not null && self.CommitBytes > 0;
            checks.Add(new { name = "process-commit", ok = processOk, pid = Environment.ProcessId, commit = self?.CommitBytes ?? 0 });
            ok &= processOk;
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "unhandled", ok = false, error = ex.ToString() });
            ok = false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { ok, at = DateTimeOffset.Now, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return ok ? 0 : 1;
    }
}
