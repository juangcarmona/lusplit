namespace LuSplit.App.Services;

/// <summary>
/// Volatile in-process store for entity IDs that have a pending conflict review.
/// Cleared once the user acknowledges the notification.
/// </summary>
public sealed class ConflictFlagStore
{
    private readonly HashSet<string> _flagged = new(StringComparer.Ordinal);

    public void Set(string entityId) => _flagged.Add(entityId);

    public bool IsSet(string entityId) => _flagged.Contains(entityId);

    public void Clear(string entityId) => _flagged.Remove(entityId);
}
