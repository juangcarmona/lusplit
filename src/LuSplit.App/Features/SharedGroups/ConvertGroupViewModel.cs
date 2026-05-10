using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ConvertGroupViewModel : ObservableObject
{
    private readonly ConvertGroupToSharedUseCase _convertUseCase;
    private readonly RefreshSharedGroupContextUseCase? _refreshUseCase;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>True when the group is already shared — skips the convert flow.</summary>
    [ObservableProperty]
    private bool _isAlreadyShared;

    public event EventHandler? ConvertCompleted;

    /// <summary>Raised when the group is already shared and no conversion is needed.</summary>
    public event EventHandler? AlreadySharedDetected;

    public ConvertGroupViewModel(
        ConvertGroupToSharedUseCase convertUseCase,
        RefreshSharedGroupContextUseCase? refreshUseCase = null)
    {
        _convertUseCase = convertUseCase;
        _refreshUseCase = refreshUseCase;
    }

    public void CheckAlreadyShared(bool isShared)
    {
        IsAlreadyShared = isShared;
        if (isShared)
            AlreadySharedDetected?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ConvertAsync(string groupId)
    {
        if (IsAlreadyShared)
        {
            ConvertCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsConverting = true;
        ErrorMessage = null;

        try
        {
            var deviceId = DeviceInfo.Current.Idiom.ToString();
            await _convertUseCase.ExecuteAsync(groupId, deviceId, CancellationToken.None);

            // FR-043i: Refresh authoritative state before navigation
            if (_refreshUseCase is not null)
            {
                try { await _refreshUseCase.ExecuteAsync(groupId); }
                catch { /* Best-effort; convert already persisted state */ }
            }

            ConvertCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsConverting = false;
        }
    }
}
