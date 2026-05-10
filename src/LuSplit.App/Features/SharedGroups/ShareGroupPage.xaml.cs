namespace LuSplit.App.Features.SharedGroups;

public sealed partial class ShareGroupPage : ContentPage, IQueryAttributable
{
    private readonly ShareGroupViewModel _viewModel;

    public ShareGroupPage(ShareGroupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var deviceId = DeviceInfo.Current.Idiom.ToString();
        var groupId = query.TryGetValue("groupId", out var gid) && !string.IsNullOrWhiteSpace(gid?.ToString())
            ? gid.ToString()
            : null;
        _viewModel.Initialize(deviceId, groupId);
    }

    private async void OnGoToGroupDetailsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");
}
