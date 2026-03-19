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

        var options = new[] { "Independent" }
            .Concat(Participants.Where(p => !string.Equals(p.Name, current.Name, StringComparison.Ordinal))
                .Select(p => p.Name))
            .ToArray();
        var selection = await DisplayActionSheetAsync("Depends on", AppResources.Common_Cancel, null, options);
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection, AppResources.Common_Cancel, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(selection, "Independent", StringComparison.Ordinal))
        {
            current.DependsOn = null;
        }
        else
        {
            // one-parent max, no simple cycle (A->B and B->A).
            var dependent = Participants.FirstOrDefault(p => string.Equals(p.Name, selection, StringComparison.Ordinal));
            if (dependent is not null && string.Equals(dependent.DependsOn, current.Name, StringComparison.Ordinal))
            {
                StatusText = "Circular dependency is not allowed.";
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
            var drafts = Participants.Select(person => new GroupDraftMember(
                person.Name,
                string.IsNullOrWhiteSpace(person.DependsOn) ? null : person.DependsOn,
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
}

public sealed class CreateParticipantViewModel : BindableObject
{
    public string Name { get; }
    public string? DependsOn { get; set; }
    public string DependsOnLabel => string.IsNullOrWhiteSpace(DependsOn) ? "Independent" : $"Depends on: {DependsOn}";

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
