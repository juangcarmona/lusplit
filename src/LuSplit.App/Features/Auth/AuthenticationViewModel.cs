using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Services;

namespace LuSplit.App.Features.Auth;

/// <summary>
/// Thin UI surface for the authentication page.
/// All durable session state lives in <see cref="SessionService"/>;
/// this ViewModel exposes it for data-binding and handles sign-in / sign-out
/// commands.
/// </summary>
public sealed partial class AuthenticationViewModel : ObservableObject
{
    private readonly SessionService _session;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsAuthenticated => _session.IsSignedIn;
    public string? Username => _session.Username;
    public string? DisplayName => _session.DisplayName;
    public string? UserId => _session.UserId;

    public event EventHandler? SignOutCompleted;

    public AuthenticationViewModel(SessionService session)
    {
        _session = session;
        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SessionService.IsSignedIn))
                OnPropertyChanged(nameof(IsAuthenticated));
            else if (e.PropertyName is nameof(SessionService.Username))
                OnPropertyChanged(nameof(Username));
            else if (e.PropertyName is nameof(SessionService.DisplayName))
                OnPropertyChanged(nameof(DisplayName));
            else if (e.PropertyName is nameof(SessionService.UserId))
                OnPropertyChanged(nameof(UserId));
        };
    }

    /// <summary>
    /// Refresh from MSAL / local store on page appear.
    /// </summary>
    [RelayCommand]
    private async Task TrySilentRefreshAsync()
    {
        await _session.RefreshAsync();
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsSigningIn = true;
        ErrorMessage = null;

        try
        {
            await _session.SignInAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
#if DEBUG
            ErrorMessage = ex.Message;
#else
            ErrorMessage = "Sign-in could not be started. Please try again.";
#endif
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
            await _session.SignOutAsync(CancellationToken.None);
            SignOutCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
