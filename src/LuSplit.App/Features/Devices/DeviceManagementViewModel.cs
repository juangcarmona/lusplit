using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Identity.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using System.Collections.ObjectModel;

namespace LuSplit.App.Features.Devices;

public sealed partial class DeviceManagementViewModel : ObservableObject
{
    private readonly IDeviceRegistrationPort _registrationPort;
    private readonly IAuthPort _authPort;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSignedIn;

    public bool NotSignedIn => !IsSignedIn;

    public ObservableCollection<DeviceDto> Devices { get; } = new();

    public DeviceManagementViewModel(IDeviceRegistrationPort registrationPort, IAuthPort authPort)
    {
        _registrationPort = registrationPort;
        _authPort = authPort;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var userId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);
            if (userId is null)
            {
                IsSignedIn = false;
                OnPropertyChanged(nameof(NotSignedIn));
                return;
            }

            IsSignedIn = true;
            OnPropertyChanged(nameof(NotSignedIn));

            var response = await _registrationPort.ListDevicesAsync(userId, CancellationToken.None);
            Devices.Clear();
            foreach (var device in response.Devices)
                Devices.Add(device);
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
    private async Task RevokeDeviceAsync(DeviceDto device)
    {
        if (device is null) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var userId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);
            if (userId is null)
            {
                ErrorMessage = "Not authenticated.";
                return;
            }

            await _registrationPort.RevokeDeviceAsync(device.DeviceId, userId, CancellationToken.None);
            Devices.Remove(device);
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
