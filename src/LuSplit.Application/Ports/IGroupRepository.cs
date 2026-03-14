using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveGroupAsync(Group group, CancellationToken cancellationToken);
}
