namespace QuiverLauncher.Core.Services;

/// <summary>
/// Helpers for backing up and restoring an app's install folder (which, for a portable app, holds its
/// saves and config too). The zip/unzip itself is ordinary file IO done by the app layer; this keeps the
/// pure, testable parts — the suggested file name and the basic safety checks.
/// </summary>
public static class InstallBackup
{
    /// <summary>A friendly default backup file name, e.g. "Army of Two v1.2 backup 2026-10-29.zip".</summary>
    public static string SuggestFileName(string? appName, string? version, DateOnly date)
    {
        var name = Sanitize(appName);
        var ver = string.IsNullOrWhiteSpace(version) ? "" : Sanitize(version.TrimStart('v', 'V'));
        var parts = new List<string> { name };
        if (!string.IsNullOrEmpty(ver)) parts.Add($"v{ver}");
        parts.Add("backup");
        parts.Add(date.ToString("yyyy-MM-dd"));
        return string.Join(" ", parts) + ".zip";
    }

    /// <summary>Replaces characters that aren't valid in a file name, collapsing runs to a single space.</summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "App";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) ? ' ' : c).ToArray());
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        cleaned = cleaned.Trim();
        return cleaned.Length == 0 ? "App" : cleaned;
    }
}
