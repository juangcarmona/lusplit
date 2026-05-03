using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ShareGroupViewModel : ObservableObject
{
    private readonly CreateSharedGroupUseCase _createSharedGroupUseCase;
    private readonly ConvertGroupToSharedUseCase? _convertUseCase;
    private string? _deviceId;
    private string? _existingGroupId;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isConvertMode;

    public bool IsCreateMode => !IsConvertMode;

    public event EventHandler<string>? GroupCreated;
    public event EventHandler? ConvertCompleted;

    public ShareGroupViewModel(CreateSharedGroupUseCase createSharedGroupUseCase, ConvertGroupToSharedUseCase? convertUseCase = null)
    {
        _createSharedGroupUseCase = createSharedGroupUseCase;
        _convertUseCase = convertUseCase;
    }

    public void Initialize(string deviceId, string? existingGroupId = null)
    {
        _deviceId = deviceId;
        _existingGroupId = existingGroupId;
        IsConvertMode = !string.IsNullOrWhiteSpace(existingGroupId) && _convertUseCase is not null;
        OnPropertyChanged(nameof(IsCreateMode));
    }

    [RelayCommand]
    private async Task CreateSharedGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(_deviceId))
        {
            ErrorMessage = "Device identifier is missing.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (IsConvertMode)
            {
                await _convertUseCase!.ExecuteAsync(
                    _existingGroupId!,
                    _deviceId,
                    CancellationToken.None);
                ConvertCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var groupId = await _createSharedGroupUseCase.ExecuteAsync(
                    Currency,
                    _deviceId,
                    CancellationToken.None);
                GroupCreated?.Invoke(this, groupId);
            }
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
