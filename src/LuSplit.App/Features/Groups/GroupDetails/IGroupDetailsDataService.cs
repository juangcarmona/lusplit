using LuSplit.App.Services.Persistence;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Features.Groups.GroupDetails;

public interface IGroupDetailsDataService
{
    Task<GroupDetailsModel> GetGroupDetailsAsync();
    Task<GroupDetailsModel> GetGroupDetailsAsync(string groupId);
    Task UpdateGroupAsync(string groupId, string groupName, string currency);
    Task ArchiveGroupAsync(string groupId);
    Task AddGroupMemberAsync(string groupId, string personName, string? householdName,
        ConsumptionCategory consumptionCategory = ConsumptionCategory.Full,
        string? customConsumptionWeight = null);
    Task UpdateGroupMemberAsync(string groupId, string participantId, string personName, string? dependsOnParticipantId);
}
