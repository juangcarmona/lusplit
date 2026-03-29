namespace LuSplit.App.Services;

public interface IGroupPageDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync(string groupId);
}
