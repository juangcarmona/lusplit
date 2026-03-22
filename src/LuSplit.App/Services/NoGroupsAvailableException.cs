namespace LuSplit.App.Services;

public sealed class NoGroupsAvailableException : InvalidOperationException
{
    public NoGroupsAvailableException()
        : base("No groups are available.")
    {
    }
}
