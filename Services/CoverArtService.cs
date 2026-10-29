using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace QuiverLauncher.Services;

/// <summary>
/// Generates cover art for an app with a text-to-image model served by ComfyUI (SDXL on the local
/// fleet by default). Catalog apps from GitHub/GitLab often ship no capsule art, so this gives them
/// a cover. Every failure returns null; callers keep the existing icon and surface a friendly note.
/// </summary>
public static class CoverArtService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    /// <summary>
    /// Renders a portrait cover for <paramref name="appName"/> and returns the PNG bytes, or null if
    /// ComfyUI could not be reached or produced nothing. <paramref name="subtitle"/> (project/author or
    /// a short description) is folded into the prompt when present.
    /// </summary>
    public static async Task<byte[]?> GenerateAsync(string appName, string? subtitle, CancellationToken token)
    {
        if (!AiConfig.HasComfyUi)
            return null;

        try
        {
            var baseUrl = AiConfig.ComfyUiUrl.TrimEnd('/');
            var clientId = Guid.NewGuid().ToString("N");
            var subject = string.IsNullOrWhiteSpace(subtitle) ? appName : $"{appName} — {subtitle}";
            var positive =
                $"video game cover art for \"{subject}\", key art, dramatic lighting, highly detailed, " +
                "digital painting, trending on artstation, vertical poster composition, no text, no watermark";
            const string negative = "text, watermark, logo, title, letters, ui, frame, border, blurry, lowres, deformed";

            var workflow = BuildWorkflow(positive, negative);
            var promptId = await QueueAsync(baseUrl, workflow, clientId, token).ConfigureAwait(false);
            if (promptId == null)
                return null;

            var image = await WaitForImageAsync(baseUrl, promptId, token).ConfigureAwait(false);
            return image;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cover generation failed: {ex.Message}");
            return null;
        }
    }

    // A standard SDXL text-to-image graph in ComfyUI /prompt (API) form, sized to a portrait cover.
    private static Dictionary<string, object> BuildWorkflow(string positive, string negative)
    {
        var seed = Random.Shared.NextInt64(0, long.MaxValue);
        return new Dictionary<string, object>
        {
            ["4"] = Node("CheckpointLoaderSimple", new { ckpt_name = AiConfig.ComfyUiCheckpoint }),
            ["5"] = Node("EmptyLatentImage", new { width = 832, height = 1216, batch_size = 1 }),
            ["6"] = Node("CLIPTextEncode", new { text = positive, clip = Ref("4", 1) }),
            ["7"] = Node("CLIPTextEncode", new { text = negative, clip = Ref("4", 1) }),
            ["3"] = Node("KSampler", new
            {
                seed,
                steps = 28,
                cfg = 7.0,
                sampler_name = "dpmpp_2m",
                scheduler = "karras",
                denoise = 1.0,
                model = Ref("4", 0),
                positive = Ref("6", 0),
                negative = Ref("7", 0),
                latent_image = Ref("5", 0),
            }),
            ["8"] = Node("VAEDecode", new { samples = Ref("3", 0), vae = Ref("4", 2) }),
            ["9"] = Node("SaveImage", new { filename_prefix = "quiver-cover", images = Ref("8", 0) }),
        };
    }

    /// <summary>
    /// Upscales an existing cover (any image bytes) ~1.5x with a light img2img hi-res pass through the
    /// SDXL checkpoint — sharper and larger with no extra upscale model needed. Returns the new PNG
    /// bytes, or null on any failure.
    /// </summary>
    public static async Task<byte[]?> UpscaleAsync(byte[] source, CancellationToken token)
    {
        if (!AiConfig.HasComfyUi || source.Length == 0)
            return null;

        try
        {
            var baseUrl = AiConfig.ComfyUiUrl.TrimEnd('/');
            var uploadedName = await UploadImageAsync(baseUrl, source, token).ConfigureAwait(false);
            if (uploadedName == null)
                return null;

            var workflow = BuildUpscaleWorkflow(uploadedName);
            var promptId = await QueueAsync(baseUrl, workflow, Guid.NewGuid().ToString("N"), token).ConfigureAwait(false);
            if (promptId == null)
                return null;

            return await WaitForImageAsync(baseUrl, promptId, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cover upscale failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> UploadImageAsync(string baseUrl, byte[] image, CancellationToken token)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "image", $"quiver-upscale-{Guid.NewGuid():N}.png");
        form.Add(new StringContent("true"), "overwrite");

        using var response = await Http.PostAsync($"{baseUrl}/upload/image", form, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
        var subfolder = doc.RootElement.TryGetProperty("subfolder", out var sf) ? sf.GetString() : "";
        if (string.IsNullOrEmpty(name))
            return null;
        return string.IsNullOrEmpty(subfolder) ? name : $"{subfolder}/{name}";
    }

    // Light hi-res img2img: encode the image, upscale in latent space, denoise a little, decode.
    private static Dictionary<string, object> BuildUpscaleWorkflow(string imageName)
    {
        var seed = Random.Shared.NextInt64(0, long.MaxValue);
        return new Dictionary<string, object>
        {
            ["4"] = Node("CheckpointLoaderSimple", new { ckpt_name = AiConfig.ComfyUiCheckpoint }),
            ["10"] = Node("LoadImage", new { image = imageName }),
            ["11"] = Node("VAEEncode", new { pixels = Ref("10", 0), vae = Ref("4", 2) }),
            ["12"] = Node("LatentUpscaleBy", new { samples = Ref("11", 0), upscale_method = "nearest-exact", scale_by = 1.5 }),
            ["6"] = Node("CLIPTextEncode", new { text = "high quality, sharp, detailed, clean", clip = Ref("4", 1) }),
            ["7"] = Node("CLIPTextEncode", new { text = "blurry, lowres, artifacts, jpeg", clip = Ref("4", 1) }),
            ["3"] = Node("KSampler", new
            {
                seed,
                steps = 18,
                cfg = 6.0,
                sampler_name = "dpmpp_2m",
                scheduler = "karras",
                denoise = 0.35,
                model = Ref("4", 0),
                positive = Ref("6", 0),
                negative = Ref("7", 0),
                latent_image = Ref("12", 0),
            }),
            ["8"] = Node("VAEDecode", new { samples = Ref("3", 0), vae = Ref("4", 2) }),
            ["9"] = Node("SaveImage", new { filename_prefix = "quiver-cover-up", images = Ref("8", 0) }),
        };
    }

    private static Dictionary<string, object> Node(string classType, object inputs) =>
        new() { ["class_type"] = classType, ["inputs"] = inputs };

    // ComfyUI wires nodes together as [sourceNodeId, outputIndex].
    private static object[] Ref(string nodeId, int output) => [nodeId, output];

    private static async Task<string?> QueueAsync(string baseUrl, Dictionary<string, object> workflow, string clientId, CancellationToken token)
    {
        var body = JsonSerializer.Serialize(new { prompt = workflow, client_id = clientId });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync($"{baseUrl}/prompt", content, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("prompt_id", out var id) ? id.GetString() : null;
    }

    private static async Task<byte[]?> WaitForImageAsync(string baseUrl, string promptId, CancellationToken token)
    {
        // Poll the history until this prompt produces an image (ComfyUI renders asynchronously).
        for (var attempt = 0; attempt < 90; attempt++)
        {
            token.ThrowIfCancellationRequested();
            using var response = await Http.GetAsync($"{baseUrl}/history/{promptId}", token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty(promptId, out var entry) &&
                    entry.TryGetProperty("outputs", out var outputs))
                {
                    foreach (var node in outputs.EnumerateObject())
                    {
                        if (!node.Value.TryGetProperty("images", out var images) || images.GetArrayLength() == 0)
                            continue;
                        var image = images[0];
                        var filename = image.GetProperty("filename").GetString();
                        var subfolder = image.TryGetProperty("subfolder", out var sf) ? sf.GetString() : "";
                        var type = image.TryGetProperty("type", out var t) ? t.GetString() : "output";
                        if (string.IsNullOrEmpty(filename))
                            continue;
                        var url = $"{baseUrl}/view?filename={Uri.EscapeDataString(filename)}" +
                                  $"&subfolder={Uri.EscapeDataString(subfolder ?? "")}&type={Uri.EscapeDataString(type ?? "output")}";
                        return await Http.GetByteArrayAsync(url, token).ConfigureAwait(false);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }

        return null;
    }
}
