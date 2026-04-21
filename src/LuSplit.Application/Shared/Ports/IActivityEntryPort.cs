using LuSplit.Domain.Activity;

namespace LuSplit.Application.Shared.Ports;

public interface IActivityEntryPort
{
    Task InsertAsync(ActivityEntry entry, CancellationToken ct);
}
