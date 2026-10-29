using System.Text.RegularExpressions;

namespace QuiverLauncher.Core.Services;

/// <summary>
/// Ranks the executable candidates found in an installed app so the launcher can pick the one most
/// likely to be the game itself, instead of asking the player to hunt for it. Pure and file-system
/// free (it works on the candidate paths and the app name), so it is cheap and easy to test, and it
/// runs with no network. An optional AI step (see the app-layer auto-selector) only gets involved
/// when this heuristic is not confident.
/// </summary>
public static class ExecutableRanker
{
    /// <summary>A ranked candidate and why it scored the way it did (handy for logging/AI prompts).</summary>
    public readonly record struct RankedExecutable(string Path, int Score, string FileName);

    /// <summary>The outcome of ranking: every candidate ordered best-first, and a confident pick when there is one.</summary>
    public readonly record struct RankResult(IReadOnlyList<RankedExecutable> Ranked, string? ConfidentPick);

    // Filename fragments that almost never name the game itself: installers, redistributables,
    // crash handlers, updaters and other helpers that ship alongside it.
    private static readonly string[] NegativeFragments =
    [
        "unins", "uninstall", "setup", "install", "vcredist", "vc_redist", "vcrun", "dxsetup",
        "dxwebsetup", "directx", "oalinst", "redist", "dotnet", "netfx", "crashhandler",
        "crashpad", "crashreport", "unitycrashhandler", "notification_helper", "nvngx",
        "update", "updater", "patch", "config", "settings", "benchmark", "server",
        "dedicated", "cleanup", "repair", "diagnostic", "report", "helper", "handler",
    ];

    // Fragments that lean towards "this is the thing you run".
    private static readonly string[] PositiveFragments = ["game", "play", "start", "launch"];

    private static readonly char[] Separators = ['/', '\\'];
    private static readonly Regex Tokenizer = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Scores and orders <paramref name="candidates"/>. <paramref name="appName"/> is the app's display
    /// name (used to reward an executable whose name matches the app). The confident pick is the clear
    /// best candidate, or <c>null</c> when the top two are too close or nothing looks like a game.
    /// </summary>
    public static RankResult Rank(IReadOnlyList<string> candidates, string? appName)
    {
        if (candidates.Count == 0)
            return new RankResult([], null);

        var appTokens = Tokenize(appName);

        var ranked = candidates
            .Select(path => new RankedExecutable(path, Score(path, appTokens), FileNameOf(path)))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => Depth(c.Path))
            .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RankResult(ranked, ConfidentPick(ranked));
    }

    private static string? ConfidentPick(IReadOnlyList<RankedExecutable> ranked)
    {
        var best = ranked[0];
        // Never auto-pick something that looks like an installer/helper.
        if (best.Score < 0)
            return null;

        // One candidate, or a clear winner over the runner-up, is safe to pick automatically.
        if (ranked.Count == 1)
            return best.Path;

        var runnerUp = ranked[1];
        return best.Score - runnerUp.Score >= 2 ? best.Path : null;
    }

    private static int Score(string path, IReadOnlyList<string> appTokens)
    {
        var fileName = FileNameOf(path);
        var stem = StemOf(fileName);
        var lower = stem.ToLowerInvariant();
        var score = 0;

        foreach (var fragment in NegativeFragments)
            if (lower.Contains(fragment, StringComparison.Ordinal))
                score -= 5;

        foreach (var fragment in PositiveFragments)
            if (lower.Contains(fragment, StringComparison.Ordinal))
                score += 1;

        // Reward names that overlap the app's own name. Compare on alphanumeric-only forms so
        // "StarshipParty.exe" still matches "Starship Party" despite the camel-casing and spacing.
        if (appTokens.Count > 0)
        {
            var appNorm = string.Concat(appTokens);
            var stemNorm = new string(lower.Where(char.IsLetterOrDigit).ToArray());
            if (stemNorm.Length > 0 &&
                (stemNorm.Contains(appNorm, StringComparison.Ordinal) || appNorm.Contains(stemNorm, StringComparison.Ordinal)))
            {
                score += 4;
            }
            else
            {
                var shared = appTokens.Count(t => stemNorm.Contains(t, StringComparison.Ordinal));
                if (shared > 0) score += 2 + shared;
            }
        }

        // Prefer files at the install root over ones buried in subfolders.
        var depth = Depth(path);
        if (depth == 0) score += 2;
        else score -= Math.Min(depth, 3);

        return score;
    }

    private static int Depth(string path) =>
        path.Count(c => c is '/' or '\\');

    private static string FileNameOf(string path)
    {
        var slash = path.LastIndexOfAny(Separators);
        return slash >= 0 ? path[(slash + 1)..] : path;
    }

    private static string StemOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static List<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        return Tokenizer.Split(value.ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .Distinct()
            .ToList();
    }
}
