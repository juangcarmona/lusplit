namespace LuSplit.App.Features.Invitations;

public partial class InvitePage : ContentPage, IQueryAttributable
{
    private readonly InviteViewModel _viewModel;

    public InvitePage(InviteViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = _viewModel;

        _viewModel.SkipRequested += OnSkipOrDone;
        _viewModel.DoneRequested += OnSkipOrDone;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var groupId = query.TryGetValue("groupId", out var gid) ? gid?.ToString() : null;
        var deviceId = query.TryGetValue("deviceId", out var did) ? did?.ToString() : string.Empty;
        var postCreate = query.TryGetValue("postCreate", out var pc) &&
                         string.Equals(pc?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        if (groupId is not null)
            _viewModel.Initialize(groupId, deviceId ?? string.Empty, postCreate);
    }

    private async void OnSkipOrDone(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
}
