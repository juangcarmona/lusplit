using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Formatting;
using LuSplit.App.Services.Localization;
using LuSplit.App.Services.Persistence;
using LuSplit.App.Services.Settings;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Features.Groups.CreateGroup;

public sealed partial class CreateGroupViewModel : ObservableObject
{
    private readonly ICreateGroupDataService _dataService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    private int _step = 1;

    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private CurrencyOption? _selectedCurrencyOption;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool IsStep1 => _step == 1;
    public bool IsStep2 => _step == 2;

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public ObservableCollection<ParticipantDraftViewModel> Participants { get; } = new();

    public event EventHandler? GroupCreated;

    public CreateGroupViewModel(ICreateGroupDataService dataService)
    {
        _dataService = dataService;
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());
    }

    [RelayCommand]
    private void Continue()
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            StatusText = AppResources.Validation_GroupNameRequired;
            return;
        }

        if (SelectedCurrencyOption is null)
        {
            StatusText = AppResources.Validation_SelectCurrency;
            return;
        }

        Step = 2;
        EnsureCurrentUserParticipant();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (Participants.Count == 0)
        {
            StatusText = AppResources.Validation_AddAtLeastOnePerson;
            return;
        }

        try
        {
            EnsureCurrentUserParticipant();

            var drafts = Participants
                .Select(person => new GroupDraftMember(
                    person.Name,
                    string.IsNullOrWhiteSpace(person.DependsOn) ? person.Name : person.DependsOn,
                    ConsumptionCategory.Full,
                    null))
                .ToArray();

            await _dataService.CreateGroupAsync(GroupName.Trim(), SelectedCurrencyOption!.Code, drafts);
            GroupCreated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public void AddParticipant(string name)
    {
        Participants.Add(new ParticipantDraftViewModel(name));
    }

    public void RemoveParticipant(ParticipantDraftViewModel participant)
    {
        Participants.Remove(participant);
    }

    public void OnDependencyChanged(ParticipantDraftViewModel participant) { }

    internal void EnsureCurrentUserParticipant()
    {
        var preferredName = UserProfilePreferences.GetPreferredName();
        var fallbackName = LocalizationHelper.GetCapitalizedMeLabel();
        var participantName = string.IsNullOrWhiteSpace(preferredName) ? fallbackName : preferredName;

        var existing = Participants
            .Select((p, i) => (p, i))
            .FirstOrDefault(x => string.Equals(x.p.Name, participantName, StringComparison.OrdinalIgnoreCase));

        if (existing.p is null)
        {
            Participants.Insert(0, new ParticipantDraftViewModel(participantName, canRemove: false));
            return;
        }

        if (existing.i > 0 || existing.p.CanRemove)
        {
            var promoted = new ParticipantDraftViewModel(existing.p.Name, canRemove: false)
            {
                DependsOn = existing.p.DependsOn
            };
            Participants.RemoveAt(existing.i);
            Participants.Insert(0, promoted);
        }
    }

    private void BuildCurrencyList(string preferredCurrencyCode)
    {
        CurrencyCatalog.PopulateSupportedOptions(CurrencyOptions);

        SelectedCurrencyOption = CurrencyCatalog.FindByCode(CurrencyOptions, preferredCurrencyCode)
            ?? CurrencyCatalog.FindByCode(CurrencyOptions, CurrencyCatalog.DefaultCurrencyCode);
    }
}
