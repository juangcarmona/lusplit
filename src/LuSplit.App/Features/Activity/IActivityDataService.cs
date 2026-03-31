using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Activity;

public interface IActivityDataService
{
    event EventHandler? DataChanged;
    Task<GroupWorkspaceModel> GetGroupWorkspaceAsync();
}
