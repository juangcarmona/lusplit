namespace LuSplit.App.Services;

public interface IHomeDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
}
