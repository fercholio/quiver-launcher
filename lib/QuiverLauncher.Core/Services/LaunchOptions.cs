namespace QuiverLauncher.Core.Services;

/// <summary>
/// Pure helpers for per-game launch options (extra command-line arguments and environment
/// variables the player sets for an app). File-system and process free, so the parsing and
/// formatting are cheap to unit-test; the app layer applies the results to a ProcessStartInfo.
/// </summary>
public static class LaunchOptions
{
    /// <summary>
    /// Splits a command-line argument string into individual tokens, honouring single and double
    /// quotes and backslash escapes (so a path with spaces can be passed as one argument). Returns
    /// an empty list for null/blank input. Throws on an unterminated quote or trailing escape so the
    /// UI can tell the player their arguments are malformed instead of silently dropping part of them.
    /// </summary>
    public static List<string> SplitArguments(string? arguments)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
            return tokens;

        var current = new System.Text.StringBuilder();
        var started = false;
        var inSingle = false;
        var inDouble = false;
        var escaping = false;

        foreach (var c in arguments)
        {
            if (escaping)
            {
                current.Append(c);
                escaping = false;
                continue;
            }

            // Backslash is an escape only outside quotes, so Windows paths inside double quotes
            // (e.g. "C:\games\mods") keep their backslashes verbatim.
            if (c == '\\' && !inSingle && !inDouble)
            {
                escaping = true;
                started = true;
                continue;
            }

            if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
                started = true;
                continue;
            }

            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                started = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingle && !inDouble)
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
                continue;
            }

            current.Append(c);
            started = true;
        }

        if (escaping || inSingle || inDouble)
            throw new FormatException("The launch arguments contain an unmatched quote or a trailing backslash.");

        if (started)
            tokens.Add(current.ToString());

        return tokens;
    }

    /// <summary>
    /// Parses environment variables entered one per line as <c>KEY=VALUE</c>. Blank lines and lines
    /// starting with <c>#</c> are ignored; the key is trimmed, the value keeps its surrounding spaces
    /// (after one optional space following the <c>=</c> is NOT stripped — values are taken verbatim).
    /// A line without <c>=</c>, or with an empty key, is skipped. Later keys win over earlier ones.
    /// </summary>
    public static Dictionary<string, string> ParseEnvironment(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            if (key.Length == 0)
                continue;

            result[key] = line[(eq + 1)..];
        }

        return result;
    }

    /// <summary>Renders an environment-variable map back to editable <c>KEY=VALUE</c> lines, sorted by key.</summary>
    public static string FormatEnvironment(IReadOnlyDictionary<string, string>? environment)
    {
        if (environment == null || environment.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            environment.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
