using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.CreateGroup;

public interface ICreateGroupDataService
{
    Task<string> CreateGroupAsync(string groupName, string currency, IReadOnlyList<GroupDraftMember> members);
}
