namespace LuSplit.App.Services;

public interface IArchivedGroupDataService
{
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync(string groupId);
}
