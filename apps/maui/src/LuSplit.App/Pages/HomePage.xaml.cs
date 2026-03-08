using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<ExpenseItemViewModel> Expenses { get; } = new();

    public ICommand RefreshCommand { get; }

    public string GroupName { get; private set; } = "LuSplit";

    public string GroupSubtitle { get; private set; } = "Shared expenses with calm clarity";

    public string SummaryText { get; private set; } = "0 expenses";

    public string OwesAmountText { get; private set; } = "$0.00";

    public string SettledAmountText { get; private set; } = "$0.00";

    public string ExpenseCountText { get; private set; } = "0 items";

    public bool IsLoading { get; private set; }

    public HomePage(AppDataService dataService)
    {
        _dataService = dataService;
        RefreshCommand = new Command(async () => await LoadAsync());

        InitializeComponent();
        BindingContext = this;

        _dataService.DataChanged += OnDataChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        OnPropertyChanged(nameof(IsLoading));

        try
        {
            var overview = await _dataService.GetOverviewAsync();

            GroupName = overview.Group.Id;
            GroupSubtitle = $"{overview.Summary.ParticipantCount} people • {overview.Summary.EconomicUnitCount} households";
            SummaryText = $"{overview.Summary.ExpenseCount} expenses";
            ExpenseCountText = $"{overview.Expenses.Count} items";

            var owes = overview.BalancesByParticipant.Where(balance => balance.AmountMinor < 0).Sum(balance => -balance.AmountMinor);
            var settled = overview.BalancesByParticipant.Where(balance => balance.AmountMinor > 0).Sum(balance => balance.AmountMinor);
            OwesAmountText = FormatMinor(owes);
            SettledAmountText = FormatMinor(settled);

            Expenses.Clear();
            foreach (var expense in overview.Expenses.OrderByDescending(expense => expense.Date, StringComparer.Ordinal))
            {
                Expenses.Add(new ExpenseItemViewModel(
                    expense.Title,
                    FormatMinor(expense.AmountMinor),
                    DateTimeOffset.Parse(expense.Date, CultureInfo.InvariantCulture).ToString("MMM d", CultureInfo.InvariantCulture),
                    $"Paid by {expense.PaidByParticipantId}"));
            }

            OnPropertyChanged(nameof(GroupName));
            OnPropertyChanged(nameof(GroupSubtitle));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(ExpenseCountText));
            OnPropertyChanged(nameof(OwesAmountText));
            OnPropertyChanged(nameof(SettledAmountText));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private static string FormatMinor(long minor)
        => string.Create(CultureInfo.InvariantCulture, $"${minor / 100.0:0.00}");

    public sealed record ExpenseItemViewModel(string Title, string Amount, string Meta, string Payer);
}
