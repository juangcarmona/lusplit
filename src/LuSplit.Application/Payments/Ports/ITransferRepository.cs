using LuSplit.Domain.Payments;

namespace LuSplit.Application.Payments.Ports;

public interface ITransferRepository
{
    Task<IReadOnlyList<Transfer>> ListTransfersByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveTransferAsync(Transfer transfer, CancellationToken cancellationToken);

    Task DeleteTransferAsync(string groupId, string transferId, CancellationToken cancellationToken);
}
