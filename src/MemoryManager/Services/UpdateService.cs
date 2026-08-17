using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Ray.MemoryManager.Services;

public sealed record ReleaseAssetInfo(string Name, long Size, string Digest, string Url, int DownloadCount)
{
    public string SizeText => Size >= 1024L * 1024 * 1024 ? $"{Size / 1024d / 1024 / 1024:0.00} GB" : $"{Size / 1024d / 1024:0.0} MB";
    public override string ToString() => $"{Name} · {SizeText}" + (string.IsNullOrWhiteSpace(Digest) ? "" : $" · {Digest}");
}

public sealed record ReleaseInfo(string Tag, string Name, string Notes, string Url, bool Prerelease, DateTimeOffset? PublishedAt, IReadOnlyList<ReleaseAssetInfo> Assets)
{
    public override string ToString() => $"{Tag} · {(Prerelease ? "Beta" : "Stable")} · {PublishedAt:yyyy-MM-dd HH:mm}";
}

public sealed record UpdateCheckResult(bool HasUpdate, string CurrentTag, ReleaseInfo? Latest, IReadOnlyList<ReleaseInfo> Releases, string Error);

public sealed class UpdateService
{
    public const string CurrentTag = "v0.9.0-beta.29-dev";
    readonly HttpClient _http = new();

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ray-MemoryManager", "0.9.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<UpdateCheckResult> CheckAsync(bool includePrereleases = true)
    {
        try
        {
            using var r = await _http.GetAsync("https://api.github.com/repos/Ray20123315/Memory_manager/releases?per_page=20").ConfigureAwait(false);
            if (!r.IsSuccessStatusCode)
                return new(false, CurrentTag, null, [], $"GitHub Releases HTTP {(int)r.StatusCode}");

            var json = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var releases = new List<ReleaseInfo>();
            foreach (var root in doc.RootElement.EnumerateArray())
            {
                if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                var prerelease = root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
                if (!includePrereleases && prerelease) continue;
                releases.Add(ParseRelease(root));
            }

            var latest = releases.FirstOrDefault();
            var hasUpdate = latest is not null && CompareTag(latest.Tag, CurrentTag) > 0;
            return new(hasUpdate, CurrentTag, latest, releases, string.Empty);
        }
        catch (Exception ex)
        {
            return new(false, CurrentTag, null, [], ex.Message);
        }
    }

    static ReleaseInfo ParseRelease(JsonElement root)
    {
        var tag = Text(root, "tag_name");
        var name = Text(root, "name");
        var notes = Text(root, "body");
        var url = Text(root, "html_url");
        var prerelease = root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
        DateTimeOffset? published = null;
        if (root.TryGetProperty("published_at", out var p) && DateTimeOffset.TryParse(p.GetString(), out var parsed)) published = parsed;
        var assets = new List<ReleaseAssetInfo>();
        if (root.TryGetProperty("assets", out var a) && a.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in a.EnumerateArray())
            {
                assets.Add(new(
                    Text(asset, "name"),
                    asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    Text(asset, "digest"),
                    Text(asset, "browser_download_url"),
                    asset.TryGetProperty("download_count", out var count) ? count.GetInt32() : 0));
            }
        }
        return new(tag, string.IsNullOrWhiteSpace(name) ? tag : name, notes, url, prerelease, published, assets);
    }

    static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    public static int CompareTag(string left, string right)
    {
        var a = ParseVersion(left);
        var b = ParseVersion(right);
        for (var i = 0; i < 4; i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    static int[] ParseVersion(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var beta = int.MaxValue;
        var betaIndex = text.IndexOf("-beta.", StringComparison.OrdinalIgnoreCase);
        if (betaIndex >= 0)
        {
            var tail = text[(betaIndex + 6)..];
            var digits = new string(tail.TakeWhile(char.IsDigit).ToArray());
            beta = int.TryParse(digits, out var n) ? n : 0;
            text = text[..betaIndex];
        }
        var parts = text.Split('.');
        return new[]
        {
            parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0,
            parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0,
            parts.Length > 2 && int.TryParse(parts[2], out var patch) ? patch : 0,
            beta
        };
    }
}
