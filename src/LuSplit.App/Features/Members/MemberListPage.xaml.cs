namespace LuSplit.App.Features.Members;

public partial class MemberListPage : ContentPage
{
    private readonly MemberListViewModel _viewModel;

    public MemberListPage(MemberListViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = _viewModel;

        _viewModel.InviteRequested += OnInviteRequested;
    }

    private async void OnInviteRequested(object? sender, string groupId)
        => await Shell.Current.GoToAsync($"{AppRoutes.Invite}?groupId={groupId}");
}
