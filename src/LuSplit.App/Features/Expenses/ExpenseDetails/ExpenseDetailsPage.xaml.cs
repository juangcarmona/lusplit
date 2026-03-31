using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Pages;

public partial class ExpenseDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ExpenseDetailsViewModel _viewModel;

    public ExpenseDetailsPage(AppDataService dataService)
    {
        _viewModel = new ExpenseDetailsViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.ExpenseDeleted += OnExpenseDeleted;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var expenseId = query.TryGetValue("expenseId", out var id)
            ? id?.ToString() ?? string.Empty
            : string.Empty;
        _viewModel.SetExpenseId(expenseId);

        if (query.TryGetValue("groupId", out var gid) && gid?.ToString() is { Length: > 0 } groupId)
            _viewModel.SetGroupId(groupId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnExpenseDeleted(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            AppResources.ExpenseDetails_DeleteTitle,
            AppResources.ExpenseDetails_DeleteMessage,
            AppResources.ExpenseDetails_DeleteAction,
            AppResources.Common_Cancel);

        if (confirm)
        {
            await _viewModel.ConfirmDeleteAsync();
        }
    }

    private void OnParticipantToggleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string id)
            _viewModel.ToggleParticipantCommand.Execute(id);
    }

    private void OnParticipantEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string id })
            _viewModel.ParticipantEditCommand.Execute(id);
    }

    private void OnParticipantRawInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is Entry { BindingContext: ExpenseParticipantRowViewModel row })
            _viewModel.OnParticipantRawInputChanged(row, e.NewTextValue ?? string.Empty);
    }
}
