using LuSplit.App.Resources.Localization;
using LuSplit.Domain.Entities;

namespace LuSplit.App.Pages;

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
