using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Ports;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveGroupAsync(Group group, CancellationToken cancellationToken);
}
