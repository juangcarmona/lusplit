using LuSplit.Domain.Activity;

namespace LuSplit.App.Features.Activity;

public interface IActivityFeedDataService
{
    Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(string groupId, int pageSize = 50, CancellationToken ct = default);
}
