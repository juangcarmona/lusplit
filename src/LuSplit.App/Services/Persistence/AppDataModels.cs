using LuSplit.Application.Groups.Models;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Services.Persistence;

public sealed record EventDraftDefaults(string? PaidByParticipantId, IReadOnlyList<string> ParticipantIds);

public sealed record GroupWorkspaceModel(
    string GroupId,
    string GroupName,
    GroupOverviewModel Overview,
    IReadOnlyDictionary<string, string> ExpenseIcons,
    DateTimeOffset? LastOpenedUtc,
    string? ImagePath = null);

public sealed record GroupListItemModel(
    string GroupId,
    string Name,
    string Currency,
    bool IsCurrent,
    string SummaryText,
    string BalancePreviewText,
    string StatusText,
    DateTimeOffset RankDate,
    string? ImagePath = null)
{
    public string AvatarInitial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public sealed record GroupDetailsModel(
    string GroupId,
    string GroupName,
    string Currency,
    bool IsArchived,
    IReadOnlyList<GroupMemberModel> Members,
    string? ImagePath = null);

public sealed record GroupMemberModel(
    string ParticipantId,
    string Name,
    string HouseholdName,
    bool IsOwner,
    string ConsumptionCategory,
    string? CustomConsumptionWeight);

public sealed record GroupDraftMember(
    string Name,
    string? HouseholdName,
    ConsumptionCategory ConsumptionCategory = ConsumptionCategory.Full,
    string? CustomConsumptionWeight = null);
