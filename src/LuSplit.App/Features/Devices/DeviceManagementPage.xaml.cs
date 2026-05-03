namespace LuSplit.App.Features.Devices;

public partial class DeviceManagementPage : ContentPage
{
    private readonly DeviceManagementViewModel _viewModel;

    public DeviceManagementPage(DeviceManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
