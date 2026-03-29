namespace LuSplit.App.Services;

public interface IActivityDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
}
