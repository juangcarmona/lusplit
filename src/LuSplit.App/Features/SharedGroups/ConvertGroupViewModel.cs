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

    public event EventHandler? ConvertCompleted;

    public ConvertGroupViewModel(ConvertGroupToSharedUseCase convertUseCase)
    {
        _convertUseCase = convertUseCase;
    }

    [RelayCommand]
    private async Task ConvertAsync(string groupId)
    {
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
