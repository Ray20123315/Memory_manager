using System.Net.Http.Headers;
using System.Text.Json;
namespace Ray.MemoryManager.Services;
public sealed class UpdateService
{
    const string CurrentTag = "v0.9.0-beta.28"; readonly HttpClient _http = new();
    public UpdateService() { _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ray-MemoryManager","0.9.0")); _http.Timeout=TimeSpan.FromSeconds(8); }
    public async Task<(bool HasUpdate,string Tag,string Notes,string Url)> CheckAsync()
    {
        using var r = await _http.GetAsync("https://api.github.com/repos/Ray20123315/Memory_manager/releases/latest");
        if (!r.IsSuccessStatusCode) return (false,CurrentTag,"目前沒有可讀取的正式 Release。", "https://github.com/Ray20123315/Memory_manager/releases");
        using var doc=JsonDocument.Parse(await r.Content.ReadAsStringAsync()); var root=doc.RootElement;
        var tag=root.TryGetProperty("tag_name",out var t)?t.GetString()??"":""; var notes=root.TryGetProperty("body",out var b)?b.GetString()??"":""; var url=root.TryGetProperty("html_url",out var u)?u.GetString()??"":"";
        return (!string.Equals(tag,CurrentTag,StringComparison.OrdinalIgnoreCase),tag,notes,url);
    }
}
