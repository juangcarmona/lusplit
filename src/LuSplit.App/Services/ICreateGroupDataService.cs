namespace LuSplit.App.Services;

public interface ICreateGroupDataService
{
    Task<string> CreateGroupAsync(string groupName, string currency, IReadOnlyList<GroupDraftMember> members);
}
