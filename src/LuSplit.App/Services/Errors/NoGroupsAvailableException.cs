namespace LuSplit.App.Services.Errors;

public sealed class NoGroupsAvailableException : InvalidOperationException
{
    public NoGroupsAvailableException()
        : base(LuSplit.App.Resources.Localization.AppResources.Startup_NoGroupsAvailable)
    {
    }
}
