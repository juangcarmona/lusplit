using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly List<EconomicUnitModel> _economicUnits = new();

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
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadParticipantsAsync();
    }

    private async Task LoadParticipantsAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        var participants = overview.Participants;
        var units = overview.EconomicUnits;
        var defaults = _dataService.GetEventDraftDefaults();
        _participants.Clear();
        _participants.AddRange(participants);
        _economicUnits.Clear();
        _economicUnits.AddRange(units);

        PayerNames.Clear();
        ParticipantOptions.Clear();

        foreach (var participant in participants)
        {
            var label = BuildParticipantLabel(participant, participants, units);
            PayerNames.Add(label);
            var isSelected = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantOptions.Add(new ParticipantOptionViewModel(participant.Id, participant.Name, label, isSelected));
        }

        SelectedPayerName = participants.FirstOrDefault(participant => string.Equals(participant.Id, defaults.PaidByParticipantId, StringComparison.Ordinal)) is { } selectedPayer
            ? BuildParticipantLabel(selectedPayer, participants, units)
            : PayerNames.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPayerName));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ExpenseTitle))
            {
                StatusText = AppResources.Validation_TitleRequired;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!TryParseAmount(AmountText, out var amountMinor))
            {
                StatusText = AppResources.Validation_InvalidAmount;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var payer = _participants.FirstOrDefault(participant =>
                string.Equals(BuildParticipantLabel(participant, _participants, _economicUnits), SelectedPayerName, StringComparison.Ordinal));
            if (payer is null)
            {
                StatusText = AppResources.Validation_SelectPayer;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var selectedParticipants = ParticipantOptions.Where(option => option.IsSelected).Select(option => option.Id).ToArray();
            if (selectedParticipants.Length == 0)
            {
                StatusText = AppResources.Validation_PickAtLeastOnePerson;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            await _dataService.AddExpenseAsync(ExpenseTitle.Trim(), amountMinor, payer.Id, ExpenseDate, selectedParticipants, null);
            StatusText = AppResources.AddEvent_Saved;
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

    private static string BuildParticipantLabel(
        ParticipantModel participant,
        IReadOnlyList<ParticipantModel> participants,
        IReadOnlyList<EconomicUnitModel> units)
    {
        var relationship = DescribeResponsibilityRelationship(participant, participants, units);
        return $"{participant.Name} ({relationship})";
    }

    private static string DescribeResponsibilityRelationship(
        ParticipantModel participant,
        IReadOnlyList<ParticipantModel> participants,
        IReadOnlyList<EconomicUnitModel> units)
    {
        var unitParticipants = participants
            .Where(candidate => string.Equals(candidate.EconomicUnitId, participant.EconomicUnitId, StringComparison.Ordinal))
            .ToArray();
        var ownerId = units.FirstOrDefault(unit => string.Equals(unit.Id, participant.EconomicUnitId, StringComparison.Ordinal))?.OwnerParticipantId;
        var owner = ownerId is null
            ? unitParticipants.FirstOrDefault()
            : unitParticipants.FirstOrDefault(candidate => string.Equals(candidate.Id, ownerId, StringComparison.Ordinal))
                ?? unitParticipants.FirstOrDefault();
        if (owner is null || unitParticipants.Length <= 1)
        {
            return AppResources.GroupDetails_ResponsibilityIndependent;
        }

        if (string.Equals(owner.Id, participant.Id, StringComparison.Ordinal))
        {
            return string.Format(AppResources.GroupDetails_ResponsibilityResponsibleForPeople, unitParticipants.Length - 1);
        }

        return string.Format(AppResources.GroupDetails_ResponsibilityDependsOn, owner.Name);
    }

    public sealed class ParticipantOptionViewModel : BindableObject
    {
        private bool _isSelected;

        public string Id { get; }

        public string Name { get; }

        public string DisplayName { get; }

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

        public ParticipantOptionViewModel(string id, string name, string displayName, bool isSelected)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            _isSelected = isSelected;
        }
    }
}
