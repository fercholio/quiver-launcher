using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuiverLauncher.Services;

/// <summary>
/// Fetches community cover art from SteamGridDB (steamgriddb.com) for an app, so games pulled from
/// GitHub/GitLab can get a real capsule image instead of a placeholder. Needs a free API key. Every
/// failure returns null so callers keep the current image.
/// </summary>
public static class SteamGridDbService
{
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Searches by name and returns the first vertical (capsule) cover's PNG/JPEG bytes, or null.</summary>
    public static async Task<byte[]?> FetchCoverAsync(string appName, CancellationToken token)
    {
        var key = AiConfig.SteamGridDbApiKey;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(appName))
            return null;

        try
        {
            var gameId = await FindGameIdAsync(key, appName, token).ConfigureAwait(false);
            if (gameId == null)
                return null;

            var imageUrl = await FirstGridUrlAsync(key, gameId.Value, token).ConfigureAwait(false);
            if (imageUrl == null)
                return null;

            return await Http.GetByteArrayAsync(imageUrl, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SteamGridDB fetch failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<int?> FindGameIdAsync(string key, string term, CancellationToken token)
    {
        using var doc = await GetJsonAsync(key, $"/search/autocomplete/{Uri.EscapeDataString(term)}", token).ConfigureAwait(false);
        if (doc == null || !doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;
        return data[0].TryGetProperty("id", out var id) ? id.GetInt32() : null;
    }

    private static async Task<string?> FirstGridUrlAsync(string key, int gameId, CancellationToken token)
    {
        // 600x900 is the standard vertical capsule; static only (no animated), and allow NSFW off by default.
        using var doc = await GetJsonAsync(key, $"/grids/game/{gameId}?dimensions=600x900&types=static&nsfw=false", token).ConfigureAwait(false);
        if (doc == null || !doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;
        return data[0].TryGetProperty("url", out var url) ? url.GetString() : null;
    }

    private static async Task<JsonDocument?> GetJsonAsync(string key, string path, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await Http.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
    }
}
