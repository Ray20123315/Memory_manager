using System.IO;
using System.Text.Json;

namespace Ray.MemoryManager.Services;

public sealed record SessionState(
    string SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset HeartbeatAt,
    bool GracefulExit,
    DateTimeOffset? EndedAt);

public sealed class SessionStateService : IDisposable
{
    readonly object _gate = new();
    readonly string _path;
    readonly System.Threading.Timer? _timer;
    SessionState _current;

    public SessionState? PreviousSession { get; }
    public SessionState Current { get { lock (_gate) return _current; } }

    public SessionStateService(string? statePath = null, bool startHeartbeat = true)
    {
        _path = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ray", "MemoryManager", "state", "session-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        PreviousSession = ReadState(_path);
        var now = DateTimeOffset.Now;
        _current = new(Guid.NewGuid().ToString("N"), now, now, false, null);
        WriteCurrent();
        if (startHeartbeat)
            _timer = new System.Threading.Timer(_ => TouchHeartbeat(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void TouchHeartbeat()
    {
        lock (_gate)
        {
            if (_current.GracefulExit) return;
            _current = _current with { HeartbeatAt = DateTimeOffset.Now };
            WriteCurrentLocked();
        }
    }

    public void MarkGracefulExit()
    {
        lock (_gate)
        {
            if (_current.GracefulExit) return;
            var now = DateTimeOffset.Now;
            _current = _current with { HeartbeatAt = now, GracefulExit = true, EndedAt = now };
            WriteCurrentLocked();
        }
    }

    public void Dispose() => _timer?.Dispose();

    void WriteCurrent()
    {
        lock (_gate) WriteCurrentLocked();
    }

    void WriteCurrentLocked()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _path, true);
    }

    static SessionState? ReadState(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }
}
