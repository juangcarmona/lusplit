namespace LuSplit.App.Services;

public interface IGroupSwitcherDataService
{
    Task<IReadOnlyList<GroupListItemModel>> GetGroupsAsync();
    Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync();
    Task SelectGroupAsync(string groupId);
}
