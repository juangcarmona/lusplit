using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Persistence;
using LuSplit.Domain.Split;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private readonly AddExpenseViewModel _viewModel;

    public AddExpensePage(AppDataService dataService)
    {
        _viewModel = new AddExpenseViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.ExpenseSaved += OnExpenseSaved;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
        MainThread.BeginInvokeOnMainThread(() => AmountEntry.Focus());
    }

    private async void OnExpenseSaved(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnParticipantCheckChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox { BindingContext: ParticipantSplitRowViewModel row })
        {
            return;
        }

        _viewModel.OnParticipantCheckedChanged(row, e.Value);
    }

    private void OnParticipantRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string participantId)
        {
            return;
        }

        _viewModel.ToggleParticipantCommand.Execute(participantId);
    }

    private void OnParticipantRawInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry { BindingContext: ParticipantSplitRowViewModel row })
        {
            return;
        }

        _viewModel.OnParticipantRawInputChanged(row, e.NewTextValue ?? string.Empty);
    }

    private async void OnModeSelectorClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantId })
        {
            return;
        }

        var result = await DisplayActionSheet(
            AppResources.AddEvent_SplitMode_Title,
            AppResources.Common_Cancel,
            null,
            AppResources.AddEvent_SplitMode_Auto,
            AppResources.AddEvent_SplitMode_Fixed,
            AppResources.AddEvent_SplitMode_Percentage);

        if (result is null || string.Equals(result, AppResources.Common_Cancel, StringComparison.Ordinal))
        {
            return;
        }

        SplitMode newMode;
        if (string.Equals(result, AppResources.AddEvent_SplitMode_Fixed, StringComparison.Ordinal))
        {
            newMode = SplitMode.Fixed;
        }
        else if (string.Equals(result, AppResources.AddEvent_SplitMode_Percentage, StringComparison.Ordinal))
        {
            newMode = SplitMode.Percentage;
        }
        else
        {
            newMode = SplitMode.Auto;
        }

        _viewModel.ApplyModeChange(participantId, newMode);
    }

    private async void OnAttachMediaClicked(object? sender, EventArgs e)
    {
        try
        {
            var pickOptions = new PickOptions { PickerTitle = AppResources.AddEvent_AttachMedia };
            var file = await FilePicker.Default.PickAsync(pickOptions);
            if (file is null)
            {
                _viewModel.SetMediaStatus(AppResources.Common_Cancel);
                return;
            }

            _viewModel.SetMediaStatus(AppResources.AddEvent_AttachMediaQueued);
        }
        catch (Exception ex)
        {
            _viewModel.SetMediaStatus(ex.Message);
        }
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var cameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraPermission != PermissionStatus.Granted)
            {
                cameraPermission = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (cameraPermission != PermissionStatus.Granted)
            {
                _viewModel.SetMediaStatus(AppResources.Common_Cancel);
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                _viewModel.SetMediaStatus(AppResources.AddEvent_CameraNotSupported);
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
            {
                _viewModel.SetMediaStatus(AppResources.Common_Cancel);
                return;
            }

            _viewModel.SetMediaStatus(AppResources.AddEvent_TakePhotoQueued);
        }
        catch (Exception ex)
        {
            _viewModel.SetMediaStatus(ex.Message);
        }
    }
}
