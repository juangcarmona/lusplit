using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : LoadOnAppearingPage
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly Dictionary<string, ParticipantModel> _participantById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownerByParticipantId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private string _currency = "USD";

    public ObservableCollection<string> PayerNames { get; } = new();
    public ObservableCollection<ParticipantOptionViewModel> ParticipantOptions { get; } = new();
    public ObservableCollection<SplitPreviewRowViewModel> SplitPreview { get; } = new();

    public string ExpenseTitle { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public string? SelectedPayerName { get; set; }
    public string StatusText { get; set; } = "";
    public bool CanSave { get; private set; }

    public AddExpensePage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override Task LoadAsync()
        => LoadParticipantsAsync();

    private async Task LoadParticipantsAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        var participants = overview.Participants;
        var unitsById = overview.EconomicUnits.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        var defaults = _dataService.GetEventDraftDefaults();
        _currency = overview.Group.Currency;
        _participants.Clear();
        _participants.AddRange(participants);
        _participantById.Clear();
        _ownerByParticipantId.Clear();

        PayerNames.Clear();
        ParticipantOptions.Clear();
        SplitPreview.Clear();
        _payerParticipantIdByLabel.Clear();

        foreach (var participant in participants)
        {
            _participantById[participant.Id] = participant;
            var ownerId = unitsById.TryGetValue(participant.EconomicUnitId, out var unit)
                ? unit.OwnerParticipantId
                : participant.Id;
            _ownerByParticipantId[participant.Id] = ownerId;
            PayerNames.Add(participant.Name);
            _payerParticipantIdByLabel[participant.Name] = participant.Id;
            var isSelected = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantOptions.Add(new ParticipantOptionViewModel(
                participant.Id,
                participant.Name,
                ownerId,
                string.Equals(ownerId, participant.Id, StringComparison.Ordinal),
                DescribeRelationshipHint(participant, participants, unitsById),
                isSelected));
        }

        SelectedPayerName = participants.FirstOrDefault(participant => string.Equals(participant.Id, defaults.PaidByParticipantId, StringComparison.Ordinal)) is { } selectedPayer
            ? selectedPayer.Name
            : PayerNames.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPayerName));
        RecalculateSplitPreview();
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

            var payer = SelectedPayerName is not null
                && _payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId)
                ? _participants.FirstOrDefault(participant => string.Equals(participant.Id, payerId, StringComparison.Ordinal))
                : null;
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
            ExpenseTitle = string.Empty;
            AmountText = string.Empty;
            OnPropertyChanged(nameof(ExpenseTitle));
            OnPropertyChanged(nameof(AmountText));
            OnPropertyChanged(nameof(StatusText));
            RecalculateSplitPreview();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnParticipantCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox { BindingContext: ParticipantOptionViewModel option })
        {
            return;
        }

        if (option.IsOwner && e.Value)
        {
            foreach (var dependent in ParticipantOptions.Where(candidate =>
                         !candidate.IsOwner && string.Equals(candidate.OwnerId, option.Id, StringComparison.Ordinal)))
            {
                dependent.IsSelected = true;
            }
        }

        RecalculateSplitPreview();
    }

    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e)
    {
        RecalculateSplitPreview();
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e)
    {
        RecalculateSplitPreview();
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

    private void RecalculateSplitPreview()
    {
        SplitPreview.Clear();
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));

        var selected = ParticipantOptions.Where(option => option.IsSelected).ToArray();
        if (selected.Length == 0 || !TryParseAmount(AmountText, out var amountMinor))
        {
            CanSave = false;
            OnPropertyChanged(nameof(CanSave));
            return;
        }

        var shareMinor = amountMinor / selected.Length;
        var remainder = (int)(amountMinor % selected.Length);

        for (var index = 0; index < selected.Length; index++)
        {
            var option = selected[index];
            var participant = _participantById[option.Id];
            var effectiveAmount = shareMinor + (index < remainder ? 1 : 0);
            var viaText = ResolveViaText(option);
            SplitPreview.Add(new SplitPreviewRowViewModel(
                participant.Name + viaText,
                FormatMinor(effectiveAmount, _currency)));
        }

        CanSave = !string.IsNullOrWhiteSpace(ExpenseTitle.Trim());
        OnPropertyChanged(nameof(CanSave));
    }

    private string ResolveViaText(ParticipantOptionViewModel participant)
    {
        if (participant.IsOwner || !_participantById.TryGetValue(participant.OwnerId, out var owner))
        {
            return string.Empty;
        }

        var ownerSelected = ParticipantOptions.Any(option =>
            string.Equals(option.Id, participant.OwnerId, StringComparison.Ordinal) && option.IsSelected);
        return ownerSelected ? string.Create(CultureInfo.CurrentCulture, $" (via {owner.Name})") : string.Empty;
    }

    private static string DescribeRelationshipHint(
        ParticipantModel participant,
        IReadOnlyList<ParticipantModel> participants,
        IReadOnlyDictionary<string, EconomicUnitModel> unitsById)
    {
        var unitParticipants = participants
            .Where(candidate => string.Equals(candidate.EconomicUnitId, participant.EconomicUnitId, StringComparison.Ordinal))
            .ToArray();
        var ownerId = unitsById.TryGetValue(participant.EconomicUnitId, out var unit)
            ? unit.OwnerParticipantId
            : participant.Id;
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

    private static string FormatMinor(long minor, string currency)
    {
        var amount = minor / 100m;
        var symbol = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => string.Empty
        };

        return string.IsNullOrEmpty(symbol)
            ? string.Create(CultureInfo.CurrentCulture, $"{amount:0.00} {currency.ToUpperInvariant()}")
            : string.Create(CultureInfo.CurrentCulture, $"{symbol}{amount:0.00}");
    }

    public sealed class ParticipantOptionViewModel : BindableObject
    {
        private bool _isSelected;

        public string Id { get; }

        public string Name { get; }

        public string OwnerId { get; }

        public bool IsOwner { get; }

        public string RelationshipHint { get; }

        public bool HasRelationshipHint => !string.IsNullOrWhiteSpace(RelationshipHint);

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

        public ParticipantOptionViewModel(
            string id,
            string name,
            string ownerId,
            bool isOwner,
            string relationshipHint,
            bool isSelected)
        {
            Id = id;
            Name = name;
            OwnerId = ownerId;
            IsOwner = isOwner;
            RelationshipHint = relationshipHint;
            _isSelected = isSelected;
        }
    }

    public sealed record SplitPreviewRowViewModel(string Name, string AmountText);
}
