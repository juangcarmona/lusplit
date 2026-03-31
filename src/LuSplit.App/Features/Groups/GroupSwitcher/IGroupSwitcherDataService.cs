using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.GroupSwitcher;

public interface IGroupSwitcherDataService
{
    Task<IReadOnlyList<GroupListItemModel>> GetGroupsAsync();
    Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync();
    Task SelectGroupAsync(string groupId);
}
