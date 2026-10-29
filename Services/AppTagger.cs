using QuiverLauncher.Models;

namespace QuiverLauncher.Services;

/// <summary>
/// Suggests short library tags for an app with the configured LLM (genre, platform, project type,
/// and the like), so the player doesn't have to tag everything by hand. Returns an empty list when
/// AI is off or unreachable, so callers simply fall back to manual tagging.
/// </summary>
public static class AppTagger
{
    public static async Task<List<string>> SuggestTagsAsync(GameInfo game, CancellationToken token)
    {
        if (!AiConfig.HasLlm)
            return [];

        const string system =
            "You tag games for a personal game library. Given a game's name and details, reply with 3 to 6 " +
            "short, lowercase tags (genre, style, platform or project type, e.g. \"platformer\", \"roguelike\", " +
            "\"n64\", \"recomp\", \"co-op\"). Reply with ONLY the tags, comma-separated, no sentences, no #.";

        var details = new List<string> { $"Name: {game.Name}" };
        if (!string.IsNullOrWhiteSpace(game.Project)) details.Add($"Project/author: {game.Project}");
        if (!string.IsNullOrWhiteSpace(game.Repository)) details.Add($"Repository: {game.Repository}");
        if (game.Tags.Count > 0) details.Add($"Existing tags: {string.Join(", ", game.Tags)}");

        var answer = await LlmClient.CompleteAsync(system, string.Join("\n", details), maxTokens: 48, token)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(answer))
            return [];

        // Models sometimes wrap the list in a sentence or quotes; keep only the comma/newline-separated terms.
        var cleaned = answer.Replace("#", "").Replace("\"", "").Replace("\n", ",");
        return TagHelper.NormalizeTags(TagHelper.ParseCommaSeparatedTags(cleaned))
            .Where(t => t.Length is >= 2 and <= 24)
            .Take(6)
            .ToList();
    }
}
