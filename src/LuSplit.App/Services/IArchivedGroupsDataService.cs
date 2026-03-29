namespace LuSplit.App.Services;

public interface IArchivedGroupsDataService
{
    event EventHandler? DataChanged;
    Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync();
}
