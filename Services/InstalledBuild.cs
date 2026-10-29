using System.Diagnostics;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Services;

/// <summary>
/// The file an app was installed from and its SHA-256, kept beside version.txt. A rolling release (a nightly) is
/// rebuilt under the same tag, so the tag alone can't tell a new build: its file's checksum can.
/// </summary>
public sealed record InstalledBuild(string FileName, string Sha256)
{
    public const string FileNameOnDisk = "installed-build.txt";

    public static InstalledBuild? Read(string gamePath)
    {
        try
        {
            var path = Path.Combine(gamePath, FileNameOnDisk);
            if (!File.Exists(path)) return null;
            var lines = File.ReadAllLines(path);
            return lines.Length >= 2 && lines[0].Trim() is { Length: > 0 } name && lines[1].Trim() is { Length: 64 } sha
                ? new(name, sha.ToLowerInvariant()) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Couldn't read the installed build in {gamePath}: {ex.Message}");
            return null;
        }
    }

    public async Task WriteAsync(string gamePath)
    {
        try { await File.WriteAllTextAsync(Path.Combine(gamePath, FileNameOnDisk), $"{FileName}\n{Sha256}\n").ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Couldn't save the installed build in {gamePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether <paramref name="release"/>, under the installed tag, now publishes a different build of the installed
    /// file. False when it doesn't list that file or its checksum, so nothing is offered on a guess.
    /// </summary>
    public bool IsRebuiltIn(GitHubRelease? release, string? installedVersion)
    {
        if (release == null || !ReleaseVersionIdentity.AreVersionsEquivalent(release.tag_name, installedVersion)) return false;
        var file = FindMatchingAsset(release, installedVersion);
        return file?.digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(digest["sha256:".Length..], Sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The release asset that is this installed file. Matches by exact name first (the common case), then
    /// by version-stripped identity when exactly one asset matches — so a nightly that renames its asset
    /// every build under the same rolling tag (client-g1a2b3c.exe → client-gdeadbee.exe) is still tracked,
    /// while an ambiguous match is left alone so nothing is offered on a guess.
    /// </summary>
    private GitHubAsset? FindMatchingAsset(GitHubRelease release, string? installedVersion)
    {
        var assets = release.assets;
        if (assets == null || assets.Length == 0) return null;

        var exact = assets.FirstOrDefault(a => string.Equals(a.name, FileName, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var installedKey = DownloadAssetPolicy.NormalizedAssetKey(FileName, installedVersion);
        var matches = assets
            .Where(a => string.Equals(
                DownloadAssetPolicy.NormalizedAssetKey(a.name, release.tag_name), installedKey, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
