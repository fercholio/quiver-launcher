using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QuiverLauncher.Services;

/// <summary>
/// Minimal client for an OpenAI-compatible chat endpoint (the LiteLLM gateway by default). It is
/// deliberately forgiving: any failure — unreachable host, bad status, odd JSON — returns null so
/// callers fall back to their non-AI path instead of surfacing an error to the player.
/// </summary>
public static class LlmClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Sends a system + user prompt and returns the assistant's text, or null if the model could not
    /// be reached or did not answer. <paramref name="maxTokens"/> keeps replies short for cheap calls.
    /// </summary>
    public static async Task<string?> CompleteAsync(string system, string user, int maxTokens, CancellationToken token)
    {
        if (!AiConfig.HasLlm)
            return null;

        try
        {
            var url = $"{AiConfig.LlmBaseUrl.TrimEnd('/')}/chat/completions";
            var payload = new
            {
                model = AiConfig.LlmModel,
                temperature = 0,
                max_tokens = maxTokens,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrEmpty(AiConfig.LlmApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AiConfig.LlmApiKey);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(AiConfig.RequestTimeout);

            using var response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LLM request failed: {ex.Message}");
            return null;
        }
    }
}
