using LuSplit.Application.Sync.UseCases;

namespace LuSplit.App.Services;

/// <summary>
/// Triggers background sync for all shared groups when the app comes to foreground.
/// Queues pending operations and exposes per-group sync status.
/// The <see cref="SyncGroupUseCase"/> is resolved lazily on first use to support
/// async initialization of the underlying SQLite infrastructure.
/// </summary>
public sealed class SyncOrchestrationService
{
    private readonly Func<Task<SyncGroupUseCase>> _useCaseFactory;
    private readonly string _deviceId;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Dictionary<string, SyncState> _syncStates = new();
    private SyncGroupUseCase? _syncUseCase;

    public event EventHandler<SyncStateChangedArgs>? SyncStateChanged;

    public SyncOrchestrationService(Func<Task<SyncGroupUseCase>> useCaseFactory, string deviceId)
    {
        _useCaseFactory = useCaseFactory;
        _deviceId = deviceId;
    }

    /// <summary>Triggers sync for all provided group IDs on app foreground.</summary>
    public async Task SyncAllAsync(IReadOnlyList<string> groupIds, CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(0, ct))
            return; // Already syncing, skip.

        try
        {
            _syncUseCase ??= await _useCaseFactory();

            foreach (var groupId in groupIds)
            {
                if (ct.IsCancellationRequested) break;

                SetState(groupId, SyncState.Syncing);
                try
                {
                    await _syncUseCase.ExecuteAsync(groupId, _deviceId, ct);
                    SetState(groupId, SyncState.UpToDate);
                }
                catch (Exception)
                {
                    SetState(groupId, SyncState.Error);
                }
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public SyncState GetState(string groupId)
        => _syncStates.TryGetValue(groupId, out var state) ? state : SyncState.Unknown;

    private void SetState(string groupId, SyncState state)
    {
        _syncStates[groupId] = state;
        SyncStateChanged?.Invoke(this, new SyncStateChangedArgs(groupId, state));
    }
}

public enum SyncState { Unknown, Syncing, UpToDate, Error }

public sealed record SyncStateChangedArgs(string GroupId, SyncState State);
