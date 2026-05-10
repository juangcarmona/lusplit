using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Services.Persistence;

/// <summary>
/// Lazy proxy that resolves <see cref="ISharedGroupStateRepository"/> from
/// <see cref="AppDataService"/> on first use, avoiding the need to synchronously
/// initialize SQLite at DI registration time.
/// </summary>
internal sealed class SharedGroupStateRepositoryProxy : ISharedGroupStateRepository
{
    private readonly AppDataService _dataService;

    public SharedGroupStateRepositoryProxy(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<SharedGroupState?> GetByGroupIdAsync(string groupId, CancellationToken ct)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        return await infra.SharedGroupStateRepository.GetByGroupIdAsync(groupId, ct);
    }

    public async Task SaveAsync(string groupId, SharedGroupState state, CancellationToken ct)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        await infra.SharedGroupStateRepository.SaveAsync(groupId, state, ct);
    }
}

/// <summary>
/// Lazy proxy that resolves <see cref="IActivityEntryPort"/> from
/// <see cref="AppDataService"/> on first use.
/// </summary>
internal sealed class ActivityEntryPortProxy : IActivityEntryPort
{
    private readonly AppDataService _dataService;

    public ActivityEntryPortProxy(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InsertAsync(LuSplit.Domain.Activity.ActivityEntry entry, CancellationToken ct)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        await infra.ActivityEntryRepository.InsertAsync(entry, ct);
    }
}

/// <summary>
/// Lazy proxy that resolves <see cref="IGroupRepository"/> from
/// <see cref="AppDataService"/> on first use.
/// </summary>
internal sealed class GroupRepositoryProxy : IGroupRepository
{
    private readonly AppDataService _dataService;

    public GroupRepositoryProxy(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        return await infra.GroupRepository.GetByIdAsync(groupId, cancellationToken);
    }

    public async Task SaveGroupAsync(Group group, CancellationToken cancellationToken)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        await infra.GroupRepository.SaveGroupAsync(group, cancellationToken);
    }
}

/// <summary>
/// Lazy proxy that resolves <see cref="IGroupMembershipRepository"/> from
/// <see cref="AppDataService"/> on first use.
/// </summary>
internal sealed class GroupMembershipRepositoryProxy : IGroupMembershipRepository
{
    private readonly AppDataService _dataService;

    public GroupMembershipRepositoryProxy(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IReadOnlyList<GroupMembership>> GetByGroupIdAsync(string groupId, CancellationToken ct)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        return await infra.GroupMembershipRepository.GetByGroupIdAsync(groupId, ct);
    }

    public async Task UpsertAsync(GroupMembership membership, CancellationToken ct)
    {
        var infra = await _dataService.GetLocalInfraAsync();
        await infra.GroupMembershipRepository.UpsertAsync(membership, ct);
    }
}
