using System.Collections.ObjectModel;
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

    public string AmountMinorText { get; set; } = string.Empty;

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
        _participants.Clear();
        _participants.AddRange(participants);

        PayerNames.Clear();
        ParticipantOptions.Clear();

        foreach (var participant in participants)
        {
            PayerNames.Add(participant.Name);
            ParticipantOptions.Add(new ParticipantOptionViewModel(participant.Id, participant.Name, true));
        }

        SelectedPayerName = PayerNames.FirstOrDefault();
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

            if (!long.TryParse(AmountMinorText, out var amountMinor) || amountMinor <= 0)
            {
                StatusText = "Amount must be a positive integer in minor units.";
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
                StatusText = "Select at least one participant for split.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            await _dataService.AddExpenseAsync(ExpenseTitle.Trim(), amountMinor, payer.Id, ExpenseDate, selectedParticipants);
            ExpenseTitle = string.Empty;
            AmountMinorText = string.Empty;
            ExpenseDate = DateTime.Today;
            StatusText = "Expense saved.";
            OnPropertyChanged(nameof(ExpenseTitle));
            OnPropertyChanged(nameof(AmountMinorText));
            OnPropertyChanged(nameof(ExpenseDate));
            OnPropertyChanged(nameof(StatusText));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
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
