using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.ArchivedGroups;

public interface IArchivedGroupsDataService
{
    event EventHandler? DataChanged;
    Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync();
}
