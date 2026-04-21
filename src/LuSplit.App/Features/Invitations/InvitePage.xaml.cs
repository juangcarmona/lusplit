namespace LuSplit.App.Features.Invitations;

public partial class InvitePage : ContentPage
{
    public InvitePage(InviteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
