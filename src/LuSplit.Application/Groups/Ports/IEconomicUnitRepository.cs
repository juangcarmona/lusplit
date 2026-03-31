using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Ports;

public interface IEconomicUnitRepository
{
    Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task<EconomicUnit?> GetEconomicUnitByIdAsync(string economicUnitId, CancellationToken cancellationToken);

    Task SaveEconomicUnitAsync(EconomicUnit economicUnit, CancellationToken cancellationToken);

    Task DeleteEconomicUnitAsync(string economicUnitId, CancellationToken cancellationToken);
}
