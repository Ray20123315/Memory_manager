using System.IO;
using System.Text.Json;
using Ray.MemoryManager.Models;

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

            var eventRead = new WindowsEventLogService().ReadRecent(TimeSpan.FromHours(2), 80);
            checks.Add(new { name = "event-log-query", ok = eventRead.ReadSucceeded, count = eventRead.Events.Count, error = eventRead.Error });
            ok &= eventRead.ReadSucceeded;

            var now = DateTimeOffset.Now;
            var syntheticPrevious = new SessionState("synthetic", now - TimeSpan.FromMinutes(30), now - TimeSpan.FromMinutes(5), false, null);
            var syntheticEvents = new List<IncidentEvent>
            {
                new(now - TimeSpan.FromMinutes(7), "Microsoft-Windows-Resource-Exhaustion-Detector", 2004, "memory-pressure", WindowsEventLogService.Summary("memory-pressure"), "Warning", string.Empty),
                new(now - TimeSpan.FromMinutes(1), "Microsoft-Windows-Kernel-Power", 41, "unexpected-restart", WindowsEventLogService.Summary("unexpected-restart"), "Critical", string.Empty)
            };
            var analysis = new PreviousCrashAnalyzer().Analyze(syntheticPrevious, syntheticEvents);
            var classifierOk = analysis.Code == "memory-pressure-correlated";
            checks.Add(new { name = "incident-classifier", ok = classifierOk, code = analysis.Code, analysis.Confidence });
            ok &= classifierOk;

            var tempRoot = Path.Combine(Path.GetTempPath(), "memory-manager-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var statePath = Path.Combine(tempRoot, "session.json");
                using (var s1 = new SessionStateService(statePath, startHeartbeat: false))
                {
                    s1.TouchHeartbeat();
                    s1.MarkGracefulExit();
                }
                using var s2 = new SessionStateService(statePath, startHeartbeat: false);
                var sessionOk = s2.PreviousSession is { GracefulExit: true };
                checks.Add(new { name = "session-heartbeat-persistence", ok = sessionOk, previous = s2.PreviousSession });
                ok &= sessionOk;

                var journalPath = Path.Combine(tempRoot, "flight.jsonl");
                var flight = new FlightRecorderService(journalPath);
                flight.Add(new MemorySample(now, 16UL * 1024 * 1024 * 1024, 8UL * 1024 * 1024 * 1024, 6UL * 1024 * 1024 * 1024, 20UL * 1024 * 1024 * 1024));
                flight.Add(new MemorySample(now + TimeSpan.FromSeconds(1), 16UL * 1024 * 1024 * 1024, 7UL * 1024 * 1024 * 1024, 7UL * 1024 * 1024 * 1024, 20UL * 1024 * 1024 * 1024));
                flight.RecordAction("self-test", "persistent-action");
                var persisted = flight.ReadPersistent(now - TimeSpan.FromSeconds(1), now + TimeSpan.FromSeconds(2));
                var flightOk = persisted.Any(x => x.Type == "frame") && persisted.Any(x => x.Type == "action");
                checks.Add(new { name = "flight-recorder-persistence", ok = flightOk, count = persisted.Count });
                ok &= flightOk;
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
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
