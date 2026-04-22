using LuSplit.App.Services.Persistence;
using LuSplit.Domain.Activity;

namespace LuSplit.App.Features.Activity;

/// <summary>
/// Provides activity feed data by delegating to the SQLite-backed
/// <see cref="LuSplit.Infrastructure.Activity.ActivityEntryRepository"/>.
/// </summary>
internal sealed class ActivityFeedDataService : IActivityFeedDataService
{
    private readonly AppDataService _dataService;

    public ActivityFeedDataService(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(
        string groupId, int pageSize = 50, CancellationToken ct = default)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        return await infra.ActivityEntryRepository.ListByGroupAsync(groupId, pageSize, ct);
    }
}
