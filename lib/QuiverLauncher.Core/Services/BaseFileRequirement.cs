namespace QuiverLauncher.Core.Services;

/// <summary>
/// Guesses whether an app expects the player to supply their own legally-owned copy of a game
/// (a ROM, ISO or other base files) — as recompilations, decompilations, ports and emulators
/// usually do — so the UI can warn before install. Pure and heuristic: it reads the project type,
/// tags and description, never the network. It never points anyone at where to obtain such files.
/// </summary>
public static class BaseFileRequirement
{
    public readonly record struct Result(bool NeedsOwnCopy, string Note);

    private static readonly string[] ProjectTypesNeedingCopy = ["port", "emulator"];

    private static readonly string[] TagsNeedingCopy =
        ["recomp", "recompilation", "decomp", "decompilation", "port", "emulator", "romhack", "rom-hack", "mod"];

    // Phrases that, in a description/README, strongly imply "bring your own game files".
    private static readonly string[] DescriptionSignals =
    [
        "provide your own", "bring your own", "your own copy", "your own legally", "legally own",
        "legal copy", "copy of the game", "copy of the original", "original game files", "base game",
        "disc image", "dump your", "dumped", "a rom ", " rom.", " rom,", "an iso", " iso ", " iso.",
        "bios", "cartridge", "game files from",
    ];

    private const string Note = "This project needs a copy of the original game you already own (a ROM/ISO or base files). Quiver doesn't provide game files.";

    public static Result Detect(string? projectType, IEnumerable<string>? tags, string? description)
    {
        var type = (projectType ?? "").Trim().ToLowerInvariant();
        if (ProjectTypesNeedingCopy.Contains(type))
            return new Result(true, Note);

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                var t = (tag ?? "").Trim().ToLowerInvariant();
                if (TagsNeedingCopy.Contains(t))
                    return new Result(true, Note);
            }
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var text = description.ToLowerInvariant();
            foreach (var signal in DescriptionSignals)
                if (text.Contains(signal, StringComparison.Ordinal))
                    return new Result(true, Note);
        }

        return new Result(false, "");
    }
}
