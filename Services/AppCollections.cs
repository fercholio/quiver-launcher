namespace QuiverLauncher.Services;

/// <summary>
/// Pure helpers for player-defined collections (categories) of library apps. Collections live in
/// <see cref="AppSettings.Collections"/> and hold app instance keys; the UI persists settings after a change.
/// </summary>
public static class AppCollections
{
    /// <summary>Finds a collection by id, or null.</summary>
    public static AppCollection? Find(AppSettings settings, string? collectionId) =>
        string.IsNullOrWhiteSpace(collectionId)
            ? null
            : settings.Collections?.FirstOrDefault(c => string.Equals(c.Id, collectionId, StringComparison.OrdinalIgnoreCase));

    public static bool IsMember(AppCollection collection, string instanceKey) =>
        !string.IsNullOrWhiteSpace(instanceKey) &&
        collection.Members.Contains(instanceKey, StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a collection with a trimmed, non-empty name, reusing one that already has that name
    /// (case-insensitive) so names stay unique. Returns the collection; does not save.</summary>
    public static AppCollection Create(AppSettings settings, string name)
    {
        settings.Collections ??= new List<AppCollection>();
        var trimmed = (name ?? "").Trim();
        var existing = settings.Collections.FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;
        var created = new AppCollection { Name = trimmed };
        settings.Collections.Add(created);
        return created;
    }

    /// <summary>Adds the key when absent, removes it when present. Returns the new membership state.</summary>
    public static bool ToggleMember(AppCollection collection, string instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey)) return false;
        var existing = collection.Members.FirstOrDefault(m => string.Equals(m, instanceKey, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            collection.Members.Remove(existing);
            return false;
        }
        collection.Members.Add(instanceKey);
        return true;
    }

    public static void SetMember(AppCollection collection, string instanceKey, bool member)
    {
        if (member == IsMember(collection, instanceKey)) return;
        ToggleMember(collection, instanceKey);
    }

    /// <summary>Drops an app from every collection (e.g. when it's removed from the library). True if anything changed.</summary>
    public static bool RemoveFromAll(AppSettings settings, string instanceKey)
    {
        if (settings.Collections == null || string.IsNullOrWhiteSpace(instanceKey)) return false;
        var changed = false;
        foreach (var collection in settings.Collections)
        {
            var existing = collection.Members.FirstOrDefault(m => string.Equals(m, instanceKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { collection.Members.Remove(existing); changed = true; }
        }
        return changed;
    }
}
