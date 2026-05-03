namespace LuSplit.App.Features.Auth;

public sealed partial class AuthenticationPage : ContentPage
{
    private readonly AuthenticationViewModel _viewModel;

    /// <summary>
    /// Tracks whether the user was already signed in when the page appeared.
    /// Used to detect the false→true transition that means "just signed in"
    /// and trigger navigation to home, while ignoring the case where the user
    /// navigated here to view their existing account.
    /// </summary>
    private bool _wasAuthenticatedOnAppear;
    private bool _isNavigating;

    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // PropertyChanged survives the browser redirect roundtrip because
        // the subscription lives on the transient page instance — no
        // unsubscribe in OnDisappearing that could be triggered when the
        // external browser comes to the foreground.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.SignOutCompleted += OnSignOutCompleted;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _wasAuthenticatedOnAppear = _viewModel.IsAuthenticated;
        _isNavigating = false;
        // Verify MSAL session is still valid and sync local account store.
        _viewModel.TrySilentRefreshCommand.Execute(null);
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AuthenticationViewModel.IsAuthenticated) || _isNavigating)
            return;

        // Detect sign-in: the user was not authenticated when the page
        // appeared and now they are. Navigate to home.
        if (_viewModel.IsAuthenticated && !_wasAuthenticatedOnAppear)
        {
            _isNavigating = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                    await Shell.Current.GoToAsync("..");
                else
                    await Shell.Current.GoToAsync("//home");
            });
        }
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