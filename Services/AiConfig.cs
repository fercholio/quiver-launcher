namespace QuiverLauncher.Services;

/// <summary>
/// Where Quiver's optional AI helpers reach their models. Everything is read from environment
/// variables so it can be pointed at any OpenAI-compatible gateway (and any ComfyUI) without a
/// rebuild; the defaults target the local AI fleet (LiteLLM gateway on node1, ComfyUI on node2).
/// Every AI feature degrades gracefully when these are unreachable, so a wrong or missing value
/// never blocks launching or installing — it just falls back to the non-AI behaviour.
/// </summary>
public static class AiConfig
{
    /// <summary>The saved settings, when loaded. Values here take precedence over environment variables.</summary>
    public static AppSettings? Settings { get; set; }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FromSettings(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool EnvFlag(string name, bool fallback)
    {
        var value = Env(name);
        if (value == null) return fallback;
        return value is "1" or "true" or "yes" or "on";
    }

    /// <summary>OpenAI-compatible chat endpoint base (…/v1). Default: the LiteLLM gateway (node1).</summary>
    public static string LlmBaseUrl => FromSettings(Settings?.AiLlmUrl) ?? Env("QUIVER_LLM_URL") ?? "http://100.72.104.59:4000/v1";

    /// <summary>Chat model name the gateway routes. Default: the 32B agent model.</summary>
    public static string LlmModel => FromSettings(Settings?.AiLlmModel) ?? Env("QUIVER_LLM_MODEL") ?? "agent-32b";

    /// <summary>Optional bearer token for the gateway.</summary>
    public static string? LlmApiKey => FromSettings(Settings?.AiLlmApiKey) ?? Env("QUIVER_LLM_KEY");

    /// <summary>ComfyUI base URL used to generate cover art. Default: node2's ComfyUI (SDXL).</summary>
    public static string ComfyUiUrl => FromSettings(Settings?.AiComfyUiUrl) ?? Env("QUIVER_COMFYUI_URL") ?? "http://100.114.215.71:8188";

    /// <summary>Checkpoint ComfyUI loads for cover art.</summary>
    public static string ComfyUiCheckpoint => FromSettings(Settings?.AiComfyUiCheckpoint) ?? Env("QUIVER_COMFYUI_CHECKPOINT") ?? "sd_xl_base_1.0.safetensors";

    /// <summary>Let the AI pick the game's executable when several are found. On by default; the heuristic runs first regardless.</summary>
    public static bool ExecutableDetectionEnabled => Settings?.AiExecutableDetectionEnabled ?? EnvFlag("QUIVER_AI_EXE", true);

    /// <summary>Optional SteamGridDB API key for fetching community cover art.</summary>
    public static string? SteamGridDbApiKey => FromSettings(Settings?.SteamGridDbApiKey) ?? Env("QUIVER_STEAMGRIDDB_KEY");

    public static bool HasSteamGridDb => !string.IsNullOrWhiteSpace(SteamGridDbApiKey);

    /// <summary>How long any single AI request may take before Quiver gives up and uses the fallback.</summary>
    public static TimeSpan RequestTimeout
    {
        get
        {
            var value = Env("QUIVER_AI_TIMEOUT_SECONDS");
            return value != null && int.TryParse(value, out var seconds) && seconds is > 0 and <= 120
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(20);
        }
    }

    public static bool HasLlm => Uri.TryCreate(LlmBaseUrl, UriKind.Absolute, out _);
    public static bool HasComfyUi => Uri.TryCreate(ComfyUiUrl, UriKind.Absolute, out _);
}
