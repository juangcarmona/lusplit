namespace LuSplit.App.Features.Activity;

public partial class ActivityFeedPage : ContentPage
{
    public ActivityFeedPage(ActivityFeedViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
