namespace LuSplit.App.Pages;

public abstract class LoadOnAppearingPage : ContentPage
{
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected abstract Task LoadAsync();
}
