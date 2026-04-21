using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Features.Auth;

/// <summary>
/// Orchestrates MSAL interactive sign-in, silent token refresh, and sign-out.
/// </summary>
public sealed partial class AuthenticationViewModel : ObservableObject
{
    private readonly IAuthPort _authPort;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isAuthenticated;

    public event EventHandler? SignInCompleted;
    public event EventHandler? SignOutCompleted;

    public AuthenticationViewModel(IAuthPort authPort)
    {
        _authPort = authPort;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsSigningIn = true;
        ErrorMessage = null;

        try
        {
            await _authPort.SignInAsync(CancellationToken.None);
            var userId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);
            IsAuthenticated = userId is not null;
            if (IsAuthenticated)
                SignInCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _authPort.SignOutAsync(CancellationToken.None);
            IsAuthenticated = false;
            SignOutCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
