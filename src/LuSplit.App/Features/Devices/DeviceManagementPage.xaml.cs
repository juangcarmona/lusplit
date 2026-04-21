namespace LuSplit.App.Features.Devices;

public partial class DeviceManagementPage : ContentPage
{
    public DeviceManagementPage(DeviceManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
