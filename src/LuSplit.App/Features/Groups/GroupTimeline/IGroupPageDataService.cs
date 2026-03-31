using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.GroupTimeline;

public interface IGroupPageDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync(string groupId);
}
