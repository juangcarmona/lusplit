using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface ITransferRepository
{
    Task<IReadOnlyList<Transfer>> ListTransfersByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveTransferAsync(Transfer transfer, CancellationToken cancellationToken);
}
