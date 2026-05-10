using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.CreateGroup;

public partial class CreateGroupPage : ContentPage
{
    private readonly CreateGroupViewModel _viewModel;

    public CreateGroupPage(AppDataService dataService)
    {
        _viewModel = new CreateGroupViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.GroupCreated += OnGroupCreated;
        _viewModel.SharedGroupCreated += OnSharedGroupCreated;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    private async void OnGroupCreated(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");

    private async void OnSharedGroupCreated(object? sender, string groupId)
    {
        // Navigate to invite flow for the newly created shared group.
        await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
        await Shell.Current.GoToAsync($"{AppRoutes.Invite}?groupId={Uri.EscapeDataString(groupId)}&postCreate=true");
    }

    private void OnParticipantAddRequested(object? sender, string name)
        => _viewModel.AddParticipant(name);

    private void OnParticipantRemoveRequested(object? sender, ParticipantDraftViewModel participant)
        => _viewModel.RemoveParticipant(participant);

    private void OnDependencyChanged(object? sender, ParticipantDraftViewModel participant)
        => _viewModel.OnDependencyChanged(participant);
}
