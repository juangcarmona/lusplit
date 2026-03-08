using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();

    public ObservableCollection<string> PayerNames { get; } = new();

    public ObservableCollection<ParticipantOptionViewModel> ParticipantOptions { get; } = new();

    public string ExpenseTitle { get; set; } = string.Empty;

    public string AmountText { get; set; } = string.Empty;

    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    public string? SelectedPayerName { get; set; }

    public string StatusText { get; set; } = "";

    public AddExpensePage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadParticipantsAsync();
    }

    private async Task LoadParticipantsAsync()
    {
        var participants = await _dataService.GetParticipantsAsync();
        var defaults = _dataService.GetEventDraftDefaults();
        _participants.Clear();
        _participants.AddRange(participants);

        PayerNames.Clear();
        ParticipantOptions.Clear();

        foreach (var participant in participants)
        {
            PayerNames.Add(participant.Name);
            var isSelected = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantOptions.Add(new ParticipantOptionViewModel(participant.Id, participant.Name, isSelected));
        }

        SelectedPayerName = participants.FirstOrDefault(participant => string.Equals(participant.Id, defaults.PaidByParticipantId, StringComparison.Ordinal))?.Name
            ?? PayerNames.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPayerName));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ExpenseTitle))
            {
                StatusText = "Title is required.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!TryParseAmount(AmountText, out var amountMinor))
            {
                StatusText = "Enter a valid amount.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var payer = _participants.FirstOrDefault(p => p.Name == SelectedPayerName);
            if (payer is null)
            {
                StatusText = "Select a payer.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var selectedParticipants = ParticipantOptions.Where(option => option.IsSelected).Select(option => option.Id).ToArray();
            if (selectedParticipants.Length == 0)
            {
                StatusText = "Pick at least one person.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            await _dataService.AddExpenseAsync(ExpenseTitle.Trim(), amountMinor, payer.Id, ExpenseDate, selectedParticipants);
            StatusText = "Event saved.";
            OnPropertyChanged(nameof(ExpenseTitle));
            OnPropertyChanged(nameof(StatusText));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnQuickChoiceClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string value } && !string.IsNullOrWhiteSpace(value))
        {
            ExpenseTitle = value;
            OnPropertyChanged(nameof(ExpenseTitle));
        }
    }

    private static bool TryParseAmount(string text, out long amountMinor)
    {
        var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
        if (decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var invariant)
            || decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out invariant))
        {
            amountMinor = (long)Math.Round(invariant * 100m, MidpointRounding.AwayFromZero);
            return amountMinor > 0;
        }

        amountMinor = 0;
        return false;
    }

    public sealed class ParticipantOptionViewModel : BindableObject
    {
        private bool _isSelected;

        public string Id { get; }

        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public ParticipantOptionViewModel(string id, string name, bool isSelected)
        {
            Id = id;
            Name = name;
            _isSelected = isSelected;
        }
    }
}
