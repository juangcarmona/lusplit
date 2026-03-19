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
    public string? SelectedCurrency { get; set; } = "USD";
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
            .Concat(Participants.Where(p => !string.Equals(p.Name, current.Name, StringComparison.Ordinal))
                .Select(p => p.Name))
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
            var orderedParticipants = Participants
                .OrderBy(person => string.IsNullOrWhiteSpace(person.DependsOn) ? 0 : 1)
                .ThenBy(person => person.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

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
        OnPropertyChanged(nameof(DependsOn));
        OnPropertyChanged(nameof(DependsOnLabel));
    }
}
