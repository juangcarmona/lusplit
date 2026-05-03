using CommunityToolkit.Mvvm.ComponentModel;
using LuSplit.App.Services.Settings;
using LuSplit.Application.Identity.UseCases;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Services;

/// <summary>
/// Single app-level source of truth for signed-in account state.
/// Bridges MSAL's token cache with <see cref="LinkedAccountStore"/> and
/// exposes observable properties that any page or ViewModel can bind to.
/// Registered as a singleton in DI — survives page navigation but not
/// process death; <see cref="RefreshAsync"/> rehydrates from MSAL + local
/// store on every app start and resume.
/// </summary>
public sealed partial class SessionService : ObservableObject
{
    private readonly IAuthPort _authPort;
    private readonly Func<RegisterDeviceUseCase> _registerDeviceFactory;

    /// <summary>
    /// True while an interactive sign-in is in progress.
    /// Prevents <see cref="RefreshAsync"/> from clearing state when
    /// <c>OnResume</c> fires before the browser-redirect flow completes.
    /// </summary>
    private bool _isInteractiveFlowActive;

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private string? _userId;

    public SessionService(IAuthPort authPort, Func<RegisterDeviceUseCase> registerDeviceFactory)
    {
        _authPort = authPort;
        _registerDeviceFactory = registerDeviceFactory;
        // Seed from persisted store so first UI frame is correct.
        LoadFromStore();
    }

    /// <summary>
    /// Query MSAL for a cached account, sync <see cref="LinkedAccountStore"/>,
    /// and update observable state. Call on app startup and resume.
    /// Skips when an interactive sign-in is in progress to avoid a race with
    /// <see cref="SignInAsync"/>.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_isInteractiveFlowActive)
            return;

        try
        {
            var user = await _authPort.GetCurrentUserAsync(CancellationToken.None);
            if (user is not null)
            {
                ApplyUser(user);
                LinkedAccountStore.Save(user.UserId, user.Username, user.DisplayName);
            }
            else if (LinkedAccountStore.HasLinkedAccount)
            {
                // MSAL has no cached account — stale local data.
                ClearState();
            }
        }
        catch
        {
            // Best-effort; if MSAL throws, fall back to local store.
            LoadFromStore();
        }
    }

    /// <summary>
    /// Perform interactive MSAL sign-in. Throws on failure (caller handles UX).
    /// Guards <see cref="RefreshAsync"/> from interfering while the browser
    /// flow is active.
    /// </summary>
    public async Task SignInAsync(CancellationToken ct)
    {
        _isInteractiveFlowActive = true;
        try
        {
            await _authPort.SignInAsync(ct);
            var user = await _authPort.GetCurrentUserAsync(ct);
            if (user is not null)
            {
                ApplyUser(user);
                LinkedAccountStore.Save(user.UserId, user.Username, user.DisplayName);

                // Register this device with the control plane after successful sign-in
                try
                {
                    var registerDevice = _registerDeviceFactory();
                    var deviceName = DeviceInfo.Current.Name;
                    var platform = DeviceInfo.Current.Platform.ToString();
                    await registerDevice.ExecuteAsync(deviceName, platform, ct);
                }
                catch (Exception ex)
                {
                    // Non-fatal: device registration failed but sign-in succeeded
                    System.Diagnostics.Debug.WriteLine($"[SessionService] Device registration failed: {ex.Message}");
                }
            }
        }
        finally
        {
            _isInteractiveFlowActive = false;
        }
    }

    /// <summary>
    /// Sign out of MSAL and clear all local account state.
    /// </summary>
    public async Task SignOutAsync(CancellationToken ct)
    {
        await _authPort.SignOutAsync(ct);
        ClearState();
    }

    private void LoadFromStore()
    {
        if (LinkedAccountStore.HasLinkedAccount)
        {
            IsSignedIn = true;
            UserId = LinkedAccountStore.UserId;
            Username = LinkedAccountStore.Username;
            DisplayName = LinkedAccountStore.DisplayName;
        }
        else
        {
            IsSignedIn = false;
            UserId = null;
            Username = null;
            DisplayName = null;
        }
    }

    private void ApplyUser(SignedInUser user)
    {
        IsSignedIn = true;
        UserId = user.UserId;
        Username = user.Username;
        DisplayName = user.DisplayName;
    }

    private void ClearState()
    {
        LinkedAccountStore.Clear();
        IsSignedIn = false;
        UserId = null;
        Username = null;
        DisplayName = null;
    }
}
