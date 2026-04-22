namespace LuSplit.App.Features.Auth;

public sealed partial class AuthenticationPage : ContentPage
{
    private readonly AuthenticationViewModel _viewModel;

    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.SignInCompleted += OnSignInCompleted;
        _viewModel.SignOutCompleted += OnSignOutCompleted;
    }

    protected override void OnDisappearing()
    {
        _viewModel.SignInCompleted -= OnSignInCompleted;
        _viewModel.SignOutCompleted -= OnSignOutCompleted;
        base.OnDisappearing();
    }

    private async void OnSignInCompleted(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//home");
        });
    }

    private async void OnSignOutCompleted(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                await Shell.Current.GoToAsync("..");
        });
    }
}