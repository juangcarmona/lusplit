using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Home;

public interface IHomeDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
}
