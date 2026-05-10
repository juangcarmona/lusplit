using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Invitations.UseCases;

namespace LuSplit.App.Features.Invitations;

public sealed partial class InviteViewModel : ObservableObject
{
    private readonly CreateInvitationUseCase _useCase;
    private string _groupId = string.Empty;
    private string _deviceId = string.Empty;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _generatedLink;

    /// <summary>True when this invite screen follows group creation (post-create onboarding).</summary>
    [ObservableProperty] private bool _isPostCreate;

    /// <summary>Raised when an invitation link is generated and ready to share.</summary>
    public event EventHandler<string>? InvitationLinkReady;

    /// <summary>Raised when the user wants to skip the invite step during post-create onboarding.</summary>
    public event EventHandler? SkipRequested;

    /// <summary>Raised when the user completes the post-create invite flow.</summary>
    public event EventHandler? DoneRequested;

    public InviteViewModel(CreateInvitationUseCase useCase)
    {
        _useCase = useCase;
    }

    public void Initialize(string groupId, string deviceId, bool postCreate = false)
    {
        _groupId = groupId;
        _deviceId = deviceId;
        IsPostCreate = postCreate;
    }

    [RelayCommand]
    private async Task GenerateInviteLinkAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        GeneratedLink = null;

        try
        {
            var response = await _useCase.ExecuteAsync(_groupId, _deviceId);
            var deepLink = BuildDeepLink(response.InvitationCode);
            GeneratedLink = deepLink;
            InvitationLinkReady?.Invoke(this, deepLink);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Skip() => SkipRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Done() => DoneRequested?.Invoke(this, EventArgs.Empty);

    private static string BuildDeepLink(string invitationCode)
        => $"lusplit://invite/{Uri.EscapeDataString(invitationCode)}";
}
