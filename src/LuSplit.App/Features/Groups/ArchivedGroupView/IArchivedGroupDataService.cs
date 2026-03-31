using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.ArchivedGroupView;

public interface IArchivedGroupDataService
{
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync(string groupId);
}
