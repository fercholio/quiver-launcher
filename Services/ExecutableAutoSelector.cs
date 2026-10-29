using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Services;

/// <summary>
/// Chooses the game's executable when several were found, so the player isn't asked to hunt for it.
/// A pure heuristic (<see cref="ExecutableRanker"/>) runs first; only when it isn't confident — and
/// only when AI is enabled and reachable — does it ask the LLM to break the tie. Returns null when
/// nothing is confident, leaving the existing "select an executable" prompt as the fallback.
/// </summary>
public static class ExecutableAutoSelector
{
    public static async Task<string?> TrySelectAsync(string appName, IReadOnlyList<string> candidates, CancellationToken token)
    {
        if (candidates.Count == 0)
            return null;
        if (candidates.Count == 1)
            return candidates[0];

        var ranked = ExecutableRanker.Rank(candidates, appName);
        if (ranked.ConfidentPick != null)
            return ranked.ConfidentPick;

        if (!AiConfig.ExecutableDetectionEnabled)
            return null;

        return await AskLlmAsync(appName, ranked.Ranked.Select(r => r.Path).ToList(), token).ConfigureAwait(false);
    }

    private static async Task<string?> AskLlmAsync(string appName, IReadOnlyList<string> candidates, CancellationToken token)
    {
        const string system =
            "You identify which file in a game's install folder is the one that launches the game. " +
            "Ignore installers, uninstallers, redistributables (VC++, DirectX, .NET), crash handlers, " +
            "updaters, servers, benchmarks and config tools. Reply with ONLY the number of the correct " +
            "file from the list, nothing else. If you cannot tell, reply 0.";

        var list = string.Join("\n", candidates.Select((path, i) => $"{i + 1}. {path}"));
        var user = $"Game: {appName}\nFiles:\n{list}\n\nWhich number is the game's executable?";

        var answer = await LlmClient.CompleteAsync(system, user, maxTokens: 8, token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(answer))
            return null;

        // Pull the first integer out of the reply (handles "2", "2.", "The answer is 2").
        var digits = new string(answer.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0)
            digits = new string(answer.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());

        if (int.TryParse(digits, out var choice) && choice >= 1 && choice <= candidates.Count)
            return candidates[choice - 1];

        // Some models echo the filename instead of the number; accept an exact match too.
        var match = candidates.FirstOrDefault(c => answer.Contains(c, StringComparison.OrdinalIgnoreCase));
        return match;
    }
}
