namespace LuSplit.App.Features.Auth;

public sealed partial class AuthenticationPage : ContentPage
{
    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
