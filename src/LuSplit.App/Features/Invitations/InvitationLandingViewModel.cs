using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Features.Invitations;

/// <summary>
/// Shows group details for a deep-linked invitation and allows accept/decline.
/// </summary>
public sealed partial class InvitationLandingViewModel : ObservableObject
{
    private readonly AcceptInvitationUseCase _acceptUseCase;
    private readonly DeclineInvitationUseCase _declineUseCase;
    private readonly IAuthPort _authPort;
    private readonly string _deviceId;

    private string _invitationCode = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private string? _invitedByDisplayName;

    [ObservableProperty]
    private DateTimeOffset _expiresAt;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isAlreadyMember;

    public event EventHandler<string>? AcceptCompleted;
    public event EventHandler? DeclineCompleted;

    public InvitationLandingViewModel(
        AcceptInvitationUseCase acceptUseCase,
        DeclineInvitationUseCase declineUseCase,
        IAuthPort authPort,
        string deviceId)
    {
        _acceptUseCase = acceptUseCase;
        _declineUseCase = declineUseCase;
        _authPort = authPort;
        _deviceId = deviceId;
    }

    public void Initialize(string invitationCode)
    {
        _invitationCode = invitationCode;
    }

    [RelayCommand]
    private async Task AcceptAsync()
    {
        if (string.IsNullOrWhiteSpace(_invitationCode)) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _authPort.SignInAsync(CancellationToken.None);

            var userId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);
            if (userId is null)
            {
                ErrorMessage = "Sign-in is required to accept an invitation.";
                return;
            }

            var deviceId = _deviceId;
            var devicePublicKey = Array.Empty<byte>(); // Will be replaced when RegisterDeviceUseCase is wired (T088).

            var result = await _acceptUseCase.ExecuteAsync(
                _invitationCode, userId, deviceId, devicePublicKey, CancellationToken.None);

            AcceptCompleted?.Invoke(this, result.GroupId);
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
    private async Task DeclineAsync()
    {
        if (string.IsNullOrWhiteSpace(_invitationCode)) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _declineUseCase.ExecuteAsync(_invitationCode, CancellationToken.None);
            DeclineCompleted?.Invoke(this, EventArgs.Empty);
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
}
