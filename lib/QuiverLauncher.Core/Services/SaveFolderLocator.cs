namespace QuiverLauncher.Core.Services;

/// <summary>
/// Guesses where a port/recomp keeps its save data, so the UI can offer "open/copy save folder"
/// (#215). Pure: it builds an ordered list of candidate directories from the install path, the app
/// name and the OS user folders; the app layer then picks the first one that actually exists on disk
/// (and falls back to the install folder). Ports vary a lot — many are portable and save inside their
/// own folder, others use AppData — so this is a best-effort ranking, not a guarantee.
/// </summary>
public static class SaveFolderLocator
{
    // Sub-directory names, in priority order, that commonly hold saves inside a portable install.
    private static readonly string[] SaveSubfolderNames =
    [
        "save", "saves", "saved", "savedata", "save_data", "savegame", "savegames",
        "saved games", "sram", "srm", "memcards", "states", "save states", "userdata",
    ];

    /// <summary>User-profile roots an app might write saves under (by its name). Any may be null/blank.</summary>
    public readonly record struct UserRoots(string? AppData, string? LocalAppData, string? Documents, string? SavedGames);

    /// <summary>
    /// Returns candidate save directories for <paramref name="installPath"/>, best guess first.
    /// Paths are built only (no disk access); duplicates and blank segments are dropped. The install
    /// folder itself is included last as a sensible fallback for fully-portable apps that save at root.
    /// </summary>
    public static List<string> Candidates(string? installPath, string? appName, UserRoots userRoots = default)
    {
        var result = new List<string>();

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }

        if (!string.IsNullOrWhiteSpace(installPath))
        {
            foreach (var name in SaveSubfolderNames)
                Add(Path.Combine(installPath, name));
        }

        var trimmedName = appName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedName))
        {
            Add(Combine(userRoots.AppData, trimmedName));
            Add(Combine(userRoots.LocalAppData, trimmedName));
            Add(Combine(userRoots.Documents, trimmedName));
            Add(Combine(userRoots.SavedGames, trimmedName));
        }

        // Fallback: the install folder itself (many recomps are portable and save at their root).
        Add(installPath);

        return result;
    }

    private static string? Combine(string? root, string name) =>
        string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, name);
}
