using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Domain.Entities;

namespace LuSplit.App.Pages;

public partial class CreateGroupPage : ContentPage
{
    private readonly AppDataService _dataService;
    private int _step = 1;

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };
    public ObservableCollection<CreateParticipantViewModel> Participants { get; } = new();

    public string GroupName { get; set; } = string.Empty;
    public string? SelectedCurrency { get; set; } = AppPreferences.GetPreferredCurrency();
    public string NewParticipantName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;

    public bool IsStep1 => _step == 1;
    public bool IsStep2 => _step == 2;

    public CreateGroupPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    private void OnContinueClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            StatusText = AppResources.Validation_GroupNameRequired;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCurrency))
        {
            StatusText = AppResources.Validation_SelectCurrency;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        _step = 2;
        EnsureCurrentUserParticipant();
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnAddParticipantClicked(object? sender, EventArgs e)
    {
        var normalized = NewParticipantName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            StatusText = AppResources.Validation_PersonNameRequired;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        if (Participants.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = AppResources.Validation_PersonNameMustBeUnique;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        Participants.Add(new CreateParticipantViewModel(normalized));
        NewParticipantName = string.Empty;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(NewParticipantName));
        OnPropertyChanged(nameof(StatusText));
    }

    private async void OnSetDependencyClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantName })
        {
            return;
        }

        var current = Participants.FirstOrDefault(p => string.Equals(p.Name, participantName, StringComparison.Ordinal));
        if (current is null)
        {
            return;
        }

        var options = new[] { AppResources.GroupDetails_DependencyIndependent }
            .Concat(Participants
                .Where(p =>
                    !string.Equals(p.Name, current.Name, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(p.DependsOn))
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var selection = await DisplayActionSheetAsync(AppResources.GroupDetails_DependsOnLabel, AppResources.Common_Cancel, null, options);
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection, AppResources.Common_Cancel, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(selection, AppResources.GroupDetails_DependencyIndependent, StringComparison.Ordinal))
        {
            current.DependsOn = null;
        }
        else
        {
            var isEligibleResponsible = Participants.Any(p =>
                string.Equals(p.Name, selection, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(p.DependsOn));
            if (!isEligibleResponsible)
            {
                StatusText = AppResources.Validation_ResponsiblePersonNotFound;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (WouldCreateCycle(current.Name, selection))
            {
                StatusText = AppResources.Validation_CircularDependency;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            current.DependsOn = selection;
        }

        current.Notify();
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (Participants.Count == 0)
        {
            StatusText = AppResources.Validation_AddAtLeastOnePerson;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        try
        {
            EnsureCurrentUserParticipant();
            var orderedParticipants = Participants.ToArray();

            var drafts = orderedParticipants.Select(person => new GroupDraftMember(
                person.Name,
                string.IsNullOrWhiteSpace(person.DependsOn) ? person.Name : person.DependsOn,
                ConsumptionCategory.Full,
                null)).ToArray();

            await _dataService.CreateGroupAsync(GroupName.Trim(), SelectedCurrency!, drafts);
            await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void EnsureCurrentUserParticipant()
    {
        var preferredName = UserProfilePreferences.GetPreferredName();
        var localizedMe = AppResources.Mapper_Me;
        var fallbackCurrentUserName = string.IsNullOrWhiteSpace(localizedMe)
            ? "Me"
            : localizedMe.Length == 1
                ? char.ToUpperInvariant(localizedMe[0]).ToString()
                : char.ToUpperInvariant(localizedMe[0]) + localizedMe[1..];
        var participantName = string.IsNullOrWhiteSpace(preferredName) ? fallbackCurrentUserName : preferredName;

        var existingIndex = Participants
            .Select((participant, index) => new { participant, index })
            .FirstOrDefault(item => string.Equals(item.participant.Name, participantName, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (existingIndex is null)
        {
            Participants.Insert(0, new CreateParticipantViewModel(participantName));
            return;
        }

        if (existingIndex.Value > 0)
        {
            var participant = Participants[existingIndex.Value];
            Participants.RemoveAt(existingIndex.Value);
            Participants.Insert(0, participant);
        }
    }

    private bool WouldCreateCycle(string participantName, string? selectedResponsible)
    {
        if (string.IsNullOrWhiteSpace(selectedResponsible))
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { participantName };
        var cursor = selectedResponsible;
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!visited.Add(cursor))
            {
                return true;
            }

            cursor = Participants.FirstOrDefault(p => string.Equals(p.Name, cursor, StringComparison.OrdinalIgnoreCase))?.DependsOn;
        }

        return false;
    }

}

public sealed class CreateParticipantViewModel : BindableObject
{
    public string Name { get; }
    public string DisplayName => UserProfilePreferences.AnnotateIfCurrentUser(Name);
    public string? DependsOn { get; set; }
    public string DependsOnLabel => string.IsNullOrWhiteSpace(DependsOn)
        ? AppResources.GroupDetails_DependencyIndependent
        : string.Format(AppResources.GroupDetails_DependencyDependsOnFormat, DependsOn);

    public CreateParticipantViewModel(string name)
    {
        Name = name;
    }

    public void Notify()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DependsOn));
        OnPropertyChanged(nameof(DependsOnLabel));
    }
}
