using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface IEconomicUnitRepository
{
    Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken);
}
