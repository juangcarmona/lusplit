using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ConvertGroupViewModel : ObservableObject
{
    private readonly ConvertGroupToSharedUseCase _convertUseCase;

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

    public ConvertGroupViewModel(ConvertGroupToSharedUseCase convertUseCase)
    {
        _convertUseCase = convertUseCase;
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
