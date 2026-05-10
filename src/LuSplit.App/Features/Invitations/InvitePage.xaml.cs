using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.Invitations;

public partial class InvitePage : ContentPage, IQueryAttributable
{
    private readonly InviteViewModel _viewModel;
    private readonly RefreshSharedGroupContextUseCase? _refreshUseCase;

    public InvitePage(InviteViewModel viewModel, RefreshSharedGroupContextUseCase? refreshUseCase = null)
    {
        _viewModel = viewModel;
        _refreshUseCase = refreshUseCase;
        InitializeComponent();
        BindingContext = _viewModel;

        _viewModel.SkipRequested += OnSkipOrDone;
        _viewModel.DoneRequested += OnSkipOrDone;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var groupId = query.TryGetValue("groupId", out var gid) ? gid?.ToString() : null;
        var deviceId = query.TryGetValue("deviceId", out var did) ? did?.ToString() : string.Empty;
        var postCreate = query.TryGetValue("postCreate", out var pc) &&
                         string.Equals(pc?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(groupId))
            return;

        _viewModel.Initialize(groupId, deviceId ?? string.Empty, postCreate);

        // FR-043i / FR-043j: Repair missing or stale shared-group state.
        // The invite flow depends on authoritative shared metadata. If we just
        // created/converted this group, the local state should already be persisted.
        // But if navigated here from a deep link or stale cache, refresh first.
        if (_refreshUseCase is not null && !postCreate)
        {
            _ = RepairSharedStateAsync(groupId);
        }
    }

    private async Task RepairSharedStateAsync(string groupId)
    {
        try
        {
            await _refreshUseCase!.ExecuteAsync(groupId);
        }
        catch
        {
            // Best-effort repair — the invite command will report its own
            // error if the group is genuinely not shared.
        }
    }

    private async void OnSkipOrDone(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
}
