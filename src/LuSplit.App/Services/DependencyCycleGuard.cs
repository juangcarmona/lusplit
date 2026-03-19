namespace LuSplit.App.Services;

public static class DependencyCycleGuard
{
    public static bool WouldCreateCycle(
        string originName,
        string? selectedResponsibleName,
        Func<string, string?> resolveNextResponsibleName)
    {
        if (string.IsNullOrWhiteSpace(selectedResponsibleName))
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { originName };
        var cursor = selectedResponsibleName;
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!visited.Add(cursor))
            {
                return true;
            }

            cursor = resolveNextResponsibleName(cursor);
        }

        return false;
    }
}
