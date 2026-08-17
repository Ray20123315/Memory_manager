using System.Diagnostics;
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

            var safety = new GameMemorySafetyPolicy(new ProcessService());
            var noGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var foregroundProtected = safety.Classify(11001, "SyntheticForeground", 11001, noGames).Protected;
            var antiCheatProtected = safety.Classify(11002, "EasyAntiCheat_EOS", 0, noGames).Protected;
            var voiceProtected = safety.Classify(11003, "Discord", 0, noGames).Protected;
            var normalAllowed = !safety.Classify(11004, "SyntheticBackgroundHelper", 0, noGames).Protected;
            var safetyOk = foregroundProtected && antiCheatProtected && voiceProtected && normalAllowed;
            checks.Add(new { name = "game-safety-invariants", ok = safetyOk, foregroundProtected, antiCheatProtected, voiceProtected, normalAllowed });
            ok &= safetyOk;

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

                var profilePath = Path.Combine(tempRoot, "game-profiles.json");
                var profiles = new GameProfileService(profilePath);
                var detected = profiles.EnsureAutoDetected(new ForegroundProcessInfo(12001, "SyntheticGame", @"C:\Games\Steam\steamapps\common\SyntheticGame\game.exe"));
                var profilesReloaded = new GameProfileService(profilePath);
                var profileOk = detected is not null && profilesReloaded.IsProfile("SyntheticGame");
                checks.Add(new { name = "game-profile-persistence", ok = profileOk, count = profilesReloaded.Profiles.Count });
                ok &= profileOk;

                var rulePath = Path.Combine(tempRoot, "memory-rules.json");
                var rules = new PerAppMemoryRuleService(rulePath);
                var addedRule = rules.AddOrEnable("SyntheticBackground", 1, 64, 2);
                var rulesReloaded = new PerAppMemoryRuleService(rulePath);
                var ruleOk = rulesReloaded.Rules.Any(x => x.Id == addedRule.Id && x.Enabled && x.TargetMemoryPriority == 2);
                checks.Add(new { name = "per-app-rule-persistence", ok = ruleOk, count = rulesReloaded.Rules.Count });
                ok &= ruleOk;

                Process? child = null;
                try
                {
                    child = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c ping 127.0.0.1 -n 20 > nul") { UseShellExecute = false, CreateNoWindow = true });
                    if (child is null) throw new InvalidOperationException("Disposable child process did not start.");
                    System.Threading.Thread.Sleep(250);
                    var priority = new ProcessMemoryPriorityService();
                    var beforeOk = priority.TryGet(child.Id, out var before, out var beforeError);
                    var target = before == 2 ? 3u : 2u;
                    var change = priority.ApplyTemporary(child.Id, target);
                    var changedOk = priority.TryGet(child.Id, out var changed, out var changedError) && changed == target;
                    var restoreCall = priority.Restore(child.Id, out var restoreMessage);
                    var restoredOk = priority.TryGet(child.Id, out var restored, out var restoredError) && restored == before;
                    var roundtripOk = beforeOk && change.Applied && changedOk && restoreCall && restoredOk;
                    checks.Add(new { name = "memory-priority-roundtrip", ok = roundtripOk, before, target, changed, restored, beforeError, changedError, restoredError, change.Message, restoreMessage });
                    ok &= roundtripOk;
                }
                finally
                {
                    if (child is not null)
                    {
                        try { if (!child.HasExited) child.Kill(true); } catch { }
                        child.Dispose();
                    }
                }
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
