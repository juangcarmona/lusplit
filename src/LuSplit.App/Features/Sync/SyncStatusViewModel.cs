using CommunityToolkit.Mvvm.ComponentModel;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Features.Sync;

public sealed partial class SyncStatusViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIconGlyph))]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private SyncStatus? _syncStatus;

    public bool IsVisible => SyncStatus is not null;

    public string StatusText => SyncStatus switch
    {
        Domain.Groups.SyncStatus.UpToDate => "Up to date",
        Domain.Groups.SyncStatus.Syncing => "Syncing…",
        Domain.Groups.SyncStatus.PendingLocalChanges => "Will update when online",
        Domain.Groups.SyncStatus.SyncError => "Sync error",
        _ => string.Empty
    };

    public string StatusIconGlyph => SyncStatus switch
    {
        Domain.Groups.SyncStatus.UpToDate => "\uf00c",       // checkmark
        Domain.Groups.SyncStatus.Syncing => "\uf021",        // refresh
        Domain.Groups.SyncStatus.PendingLocalChanges => "\uf110", // spinner / offline
        Domain.Groups.SyncStatus.SyncError => "\uf071",      // warning
        _ => string.Empty
    };
}
