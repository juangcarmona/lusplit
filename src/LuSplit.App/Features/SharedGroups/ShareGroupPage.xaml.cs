namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ShareGroupPage : ContentPage
{
    public ShareGroupPage(ShareGroupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
