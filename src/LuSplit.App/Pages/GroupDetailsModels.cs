using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Domain.Entities;

namespace LuSplit.App.Pages;

/// <summary>
/// Shared view model used by both CreateGroupPage (draft) and GroupDetailsPage (edit).
/// When <see cref="ParticipantId"/> is <c>null</c> the instance is a draft not yet persisted.
/// </summary>
public sealed class ParticipantDraftViewModel : BindableObject
{
    public string? ParticipantId { get; }
    public string Name { get; }
    public bool CanRemove { get; }
    public string DisplayName => UserProfilePreferences.AnnotateIfCurrentUser(Name);

    public string? DependsOn { get; set; }

    public string DependsOnLabel => string.IsNullOrWhiteSpace(DependsOn)
        ? AppResources.GroupDetails_DependencyIndependent
        : string.Format(AppResources.GroupDetails_DependencyDependsOnFormat, DependsOn);

    public ParticipantDraftViewModel(string name, string? participantId = null, bool canRemove = true)
    {
        Name = name;
        ParticipantId = participantId;
        CanRemove = canRemove;
    }

    public void Notify()
    {
        OnPropertyChanged(nameof(DependsOn));
        OnPropertyChanged(nameof(DependsOnLabel));
        OnPropertyChanged(nameof(DisplayName));
    }
}

public sealed record GroupPersonEditorViewModel(
    string? ParticipantId,
    string Name,
    string? HouseholdName,
    bool CanRemove,
    string RelationshipText,
    bool IsDependent,
    bool IsOwner,
    string ConsumptionCategory = "FULL",
    string? CustomConsumptionWeight = null)
{
    public string ConsumptionLabel => ConsumptionCategory switch
    {
        "HALF" => AppResources.GroupDetails_ConsumptionHalf,
        "CUSTOM" => $"{AppResources.GroupDetails_ConsumptionCustom}: {CustomConsumptionWeight}",
        _ => AppResources.GroupDetails_ConsumptionFull
    };

    public string DependencyText => RelationshipText;
    public string DisplayName => Services.UserProfilePreferences.AnnotateIfCurrentUser(Name);
}

public sealed record ConsumptionOptionViewModel(ConsumptionCategory Category, string Label);
