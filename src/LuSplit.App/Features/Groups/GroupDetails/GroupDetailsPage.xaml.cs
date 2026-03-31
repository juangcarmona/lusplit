using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Export;
using LuSplit.App.Services.Media;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.GroupDetails;

public partial class GroupDetailsPage : ContentPage, IQueryAttributable
{
    private readonly GroupDetailsViewModel _viewModel;
    private readonly GroupPhotoService _photoService;
    private readonly AppDataService _dataService;

    public GroupDetailsPage(AppDataService dataService)
    {
        _dataService = dataService;
        _photoService = new GroupPhotoService(dataService);
        _viewModel = new GroupDetailsViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.SaveCompleted += OnSaveCompleted;
        _viewModel.ArchiveConfirmationRequested += OnArchiveConfirmationRequested;
        _viewModel.ArchiveCompleted += OnArchiveCompleted;
        _viewModel.ExportRequested += OnExportRequested;
        _viewModel.PhotoChangeRequested += OnPhotoChangeRequested;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var groupId = query.TryGetValue("groupId", out var gid) && !string.IsNullOrWhiteSpace(gid?.ToString())
            ? gid.ToString()
            : null;
        _viewModel.SetOverrideGroupId(groupId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnParticipantAddRequested(object? sender, string name)
        => await _viewModel.AddMemberAsync(name);

    private async void OnDependencyChanged(object? sender, ParticipantDraftViewModel participant)
        => await _viewModel.UpdateMemberDependencyAsync(participant);

    private async void OnSaveCompleted(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnArchiveConfirmationRequested(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            AppResources.GroupDetails_ArchiveConfirmTitle,
            AppResources.GroupDetails_ArchiveConfirmMessage,
            AppResources.GroupDetails_ArchiveConfirmYes,
            AppResources.Common_Cancel);

        if (confirmed)
            await _viewModel.ConfirmArchiveAsync();
    }

    private async void OnArchiveCompleted(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");

    private async void OnExportRequested(object? sender, string groupId)
    {
        try
        {
            await GroupExportService.RunExportFlowAsync(this, _dataService, groupId);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = string.Format(AppResources.Export_Failed, ex.Message);
        }
    }

    private async void OnPhotoChangeRequested(object? sender, EventArgs e)
    {
        var groupId = _viewModel.GroupId;
        if (groupId is null) return;

        var choice = await DisplayActionSheetAsync(
            AppResources.GroupDetails_PhotoSectionTitle,
            AppResources.Common_Cancel,
            null,
            AppResources.GroupDetails_PhotoFromCamera,
            AppResources.GroupDetails_PhotoFromGallery,
            string.IsNullOrEmpty(_viewModel.GroupImagePath) ? null : AppResources.GroupDetails_PhotoRemove);

        if (string.IsNullOrEmpty(choice) || choice == AppResources.Common_Cancel)
            return;

        try
        {
            if (choice == AppResources.GroupDetails_PhotoRemove)
            {
                await _photoService.RemoveAsync(groupId, _viewModel.GroupImagePath);
                _viewModel.ApplyPhotoRemoved();
                return;
            }

            var destPath = await _photoService.PickAndSaveAsync(groupId, choice == AppResources.GroupDetails_PhotoFromCamera);
            if (destPath is null) return;
            _viewModel.ApplyNewPhoto(destPath);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = ex.Message;
        }
    }
}
