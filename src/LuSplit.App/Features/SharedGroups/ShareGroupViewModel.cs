using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ShareGroupViewModel : ObservableObject
{
    private readonly CreateSharedGroupUseCase _createSharedGroupUseCase;
    private readonly ConvertGroupToSharedUseCase? _convertUseCase;
    private readonly ISharedGroupStateRepository? _sharedStateRepo;
    private string? _deviceId;
    private string? _existingGroupId;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreateMode))]
    private bool _isConvertMode;

    [ObservableProperty]
    private bool _isAlreadyShared;

    public bool IsCreateMode => !IsConvertMode && !IsAlreadyShared;

    public event EventHandler<string>? GroupCreated;
    public event EventHandler? ConvertCompleted;

    public ShareGroupViewModel(CreateSharedGroupUseCase createSharedGroupUseCase, ConvertGroupToSharedUseCase? convertUseCase = null, ISharedGroupStateRepository? sharedStateRepo = null)
    {
        _createSharedGroupUseCase = createSharedGroupUseCase;
        _convertUseCase = convertUseCase;
        _sharedStateRepo = sharedStateRepo;
    }

    public async void Initialize(string deviceId, string? existingGroupId = null)
    {
        _deviceId = deviceId;
        _existingGroupId = existingGroupId;

        // Check if the group is already shared
        if (!string.IsNullOrWhiteSpace(existingGroupId) && _sharedStateRepo is not null)
        {
            var sharedState = await _sharedStateRepo.GetByGroupIdAsync(existingGroupId, CancellationToken.None);
            if (sharedState is not null)
            {
                IsAlreadyShared = true;
                IsConvertMode = false;
                return;
            }
        }

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
