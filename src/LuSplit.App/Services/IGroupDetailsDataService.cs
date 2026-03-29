using LuSplit.Domain.Entities;

namespace LuSplit.App.Services;

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
