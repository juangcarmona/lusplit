namespace LuSplit.App.Services;

public sealed class NoGroupsAvailableException : InvalidOperationException
{
    public NoGroupsAvailableException()
        : base(LuSplit.App.Resources.Localization.AppResources.Startup_NoGroupsAvailable)
    {
    }
}
