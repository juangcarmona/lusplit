namespace LuSplit.App.Features.Invitations;

public sealed partial class InvitationLandingPage : ContentPage
{
    public InvitationLandingPage(InvitationLandingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
