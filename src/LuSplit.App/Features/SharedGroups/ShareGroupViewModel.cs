using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.UseCases;

namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ShareGroupViewModel : ObservableObject
{
    private readonly CreateSharedGroupUseCase _createSharedGroupUseCase;
    private string? _deviceId;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public event EventHandler<string>? GroupCreated;

    public ShareGroupViewModel(CreateSharedGroupUseCase createSharedGroupUseCase)
    {
        _createSharedGroupUseCase = createSharedGroupUseCase;
    }

    public void Initialize(string deviceId)
    {
        _deviceId = deviceId;
    }

    [RelayCommand]
    private async Task CreateSharedGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(_deviceId))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var groupId = await _createSharedGroupUseCase.ExecuteAsync(
                Currency,
                _deviceId,
                CancellationToken.None);

            GroupCreated?.Invoke(this, groupId);
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
