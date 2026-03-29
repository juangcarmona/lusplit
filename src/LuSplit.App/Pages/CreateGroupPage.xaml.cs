using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class CreateGroupPage : ContentPage
{
    private readonly CreateGroupViewModel _viewModel;

    public CreateGroupPage(AppDataService dataService)
    {
        _viewModel = new CreateGroupViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.GroupCreated += OnGroupCreated;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    private async void OnGroupCreated(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");

    private void OnParticipantAddRequested(object? sender, string name)
        => _viewModel.AddParticipant(name);

    private void OnParticipantRemoveRequested(object? sender, ParticipantDraftViewModel participant)
        => _viewModel.RemoveParticipant(participant);

    private void OnDependencyChanged(object? sender, ParticipantDraftViewModel participant)
        => _viewModel.OnDependencyChanged(participant);
}

