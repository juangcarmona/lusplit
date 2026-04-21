namespace LuSplit.App.Features.Members;

public partial class MemberListPage : ContentPage
{
    public MemberListPage(MemberListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
