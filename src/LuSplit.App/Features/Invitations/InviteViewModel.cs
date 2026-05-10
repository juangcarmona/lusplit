using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Features.Invitations;

/// <summary>
/// Invite flow states. The primary interaction generates a link and
/// immediately opens the share sheet — no raw link as primary UI.
/// </summary>
public enum InviteFlowState
{
    Initial,
    Generating,
    LinkReady,
    Sharing,
    ShareCompleted,
    ShareCancelled,
    ShareFailed,
}

public sealed partial class InviteViewModel : ObservableObject
{
    private readonly CreateInvitationUseCase _useCase;
    private readonly IShareSheetPort? _shareSheet;
    private readonly IClipboardPort? _clipboard;
    private string _groupId = string.Empty;
    private string _deviceId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(ShowLinkSection))]
    [NotifyPropertyChangedFor(nameof(ShowFallbackActions))]
    [NotifyPropertyChangedFor(nameof(CanInvite))]
    private InviteFlowState _flowState = InviteFlowState.Initial;

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _generatedLink;

    /// <summary>True when this invite screen follows group creation (post-create onboarding).</summary>
    [ObservableProperty] private bool _isPostCreate;

    // Derived state
    public bool IsLoading => FlowState is InviteFlowState.Generating or InviteFlowState.Sharing;
    public bool ShowLinkSection => GeneratedLink is not null
        && FlowState is not InviteFlowState.Initial and not InviteFlowState.Generating;
    public bool ShowFallbackActions => FlowState is InviteFlowState.ShareCancelled
        or InviteFlowState.ShareFailed or InviteFlowState.ShareCompleted or InviteFlowState.LinkReady;
    public bool CanInvite => FlowState is InviteFlowState.Initial or InviteFlowState.ShareCompleted
        or InviteFlowState.ShareCancelled or InviteFlowState.ShareFailed;

    /// <summary>Raised when the user wants to skip the invite step during post-create onboarding.</summary>
    public event EventHandler? SkipRequested;

    /// <summary>Raised when the user completes the post-create invite flow.</summary>
    public event EventHandler? DoneRequested;

    public InviteViewModel(CreateInvitationUseCase useCase,
        IShareSheetPort? shareSheet = null,
        IClipboardPort? clipboard = null)
    {
        _useCase = useCase;
        _shareSheet = shareSheet;
        _clipboard = clipboard;
    }

    public void Initialize(string groupId, string deviceId, bool postCreate = false)
    {
        _groupId = groupId;
        _deviceId = deviceId;
        IsPostCreate = postCreate;
    }

    /// <summary>
    /// Primary command: generate invite link and immediately open the share sheet.
    /// FR-015b: share sheet first, raw link as secondary fallback only.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInvite))]
    private async Task InviteAsync()
    {
        FlowState = InviteFlowState.Generating;
        ErrorMessage = null;
        GeneratedLink = null;

        try
        {
            var response = await _useCase.ExecuteAsync(_groupId, _deviceId);
            var deepLink = BuildDeepLink(response.InvitationCode);
            GeneratedLink = deepLink;
            OnPropertyChanged(nameof(ShowLinkSection));

            // Immediately open the share sheet — FR-015b
            FlowState = InviteFlowState.Sharing;
            if (_shareSheet is not null)
            {
                var shared = await _shareSheet.ShareTextAsync(
                    "Join my group on LuSplit",
                    deepLink);
                FlowState = shared
                    ? InviteFlowState.ShareCompleted
                    : InviteFlowState.ShareCancelled;
            }
            else
            {
                // No share sheet available — fall back to link-ready state
                FlowState = InviteFlowState.LinkReady;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            FlowState = GeneratedLink is not null
                ? InviteFlowState.ShareFailed
                : InviteFlowState.Initial;
        }
    }

    /// <summary>
    /// Copy the generated invitation link to clipboard.
    /// FR-015c: fallback when share sheet is dismissed.
    /// </summary>
    [RelayCommand]
    private async Task CopyInviteLinkAsync()
    {
        if (GeneratedLink is null) return;

        if (_clipboard is not null)
        {
            await _clipboard.SetTextAsync(GeneratedLink);
        }
    }

    /// <summary>
    /// Retry sharing the already-generated link via the share sheet.
    /// FR-015c: retry sharing from the same screen.
    /// </summary>
    [RelayCommand]
    private async Task ShareAgainAsync()
    {
        if (GeneratedLink is null || _shareSheet is null) return;

        FlowState = InviteFlowState.Sharing;

        try
        {
            var shared = await _shareSheet.ShareTextAsync(
                "Join my group on LuSplit",
                GeneratedLink);
            FlowState = shared
                ? InviteFlowState.ShareCompleted
                : InviteFlowState.ShareCancelled;
        }
        catch
        {
            FlowState = InviteFlowState.ShareFailed;
        }
    }

    [RelayCommand]
    private void Skip() => SkipRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Done() => DoneRequested?.Invoke(this, EventArgs.Empty);

    private static string BuildDeepLink(string invitationCode)
        => $"lusplit://invite/{Uri.EscapeDataString(invitationCode)}";
}
