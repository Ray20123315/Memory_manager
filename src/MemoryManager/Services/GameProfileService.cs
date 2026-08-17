using System.IO;
using System.Text.Json;

namespace Ray.MemoryManager.Services;

public sealed record GameProfile(string ProcessName, string DisplayName, string Source, bool Enabled, DateTimeOffset CreatedAt);

public sealed class GameProfileService
{
    static readonly string[] GamePathMarkers =
    {
        "\\steamapps\\common\\", "\\xboxgames\\", "\\epic games\\", "\\riot games\\", "\\gog galaxy\\games\\"
    };
    static readonly HashSet<string> LauncherNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamwebhelper", "epicgameslauncher", "riotclientservices", "goggalaxy", "gamingservices", "explorer"
    };

    readonly string _path;
    readonly List<GameProfile> _profiles;

    public IReadOnlyList<GameProfile> Profiles => _profiles;

    public GameProfileService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ray", "MemoryManager", "settings", "game-profiles.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _profiles = Load(_path);
    }

    public GameProfile? EnsureAutoDetected(ForegroundProcessInfo foreground)
    {
        if (foreground.Pid <= 0 || string.IsNullOrWhiteSpace(foreground.Name)) return null;
        var existing = _profiles.FirstOrDefault(x => string.Equals(x.ProcessName, foreground.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        if (!LooksLikeGame(foreground)) return null;
        var profile = new GameProfile(foreground.Name, foreground.Name, "auto-path", true, DateTimeOffset.Now);
        _profiles.Add(profile);
        Save();
        return profile;
    }

    public GameProfile AddManual(ForegroundProcessInfo foreground)
    {
        if (foreground.Pid <= 0 || string.IsNullOrWhiteSpace(foreground.Name))
            throw new InvalidOperationException("目前沒有可加入的前景程式。");
        var existing = _profiles.FirstOrDefault(x => string.Equals(x.ProcessName, foreground.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var profile = new GameProfile(foreground.Name, foreground.Name, "manual", true, DateTimeOffset.Now);
        _profiles.Add(profile);
        Save();
        return profile;
    }

    public bool IsProfile(string processName) => _profiles.Any(x => x.Enabled && string.Equals(x.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

    public void SetEnabled(string processName, bool enabled)
    {
        var index = _profiles.FindIndex(x => string.Equals(x.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        _profiles[index] = _profiles[index] with { Enabled = enabled };
        Save();
    }

    public static bool LooksLikeGame(ForegroundProcessInfo foreground)
    {
        if (LauncherNames.Contains(foreground.Name)) return false;
        if (string.IsNullOrWhiteSpace(foreground.Path)) return false;
        var path = foreground.Path.ToLowerInvariant();
        return GamePathMarkers.Any(path.Contains);
    }

    void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _path, true);
    }

    static List<GameProfile> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<GameProfile>>(File.ReadAllText(path)) ?? [];
        }
        catch { return []; }
    }
}
