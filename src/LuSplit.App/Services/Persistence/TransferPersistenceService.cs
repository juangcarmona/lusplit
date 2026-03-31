using LuSplit.Application.Payments.Commands;
using LuSplit.Application.Shared.Commands;
using LuSplit.Infrastructure.Sqlite;

namespace LuSplit.App.Services.Persistence;

/// <summary>
/// Handles persistence for manual payment transfers.
/// </summary>
internal sealed class TransferPersistenceService
{
    private readonly Func<Task<InfraLocalSqlite>> _getInfra;
    private readonly Func<Task<string>> _getSelectedGroupId;

    internal TransferPersistenceService(
        Func<Task<InfraLocalSqlite>> getInfra,
        Func<Task<string>> getSelectedGroupId)
    {
        _getInfra = getInfra;
        _getSelectedGroupId = getSelectedGroupId;
    }

    internal async Task AddPaymentAsync(
        string fromParticipantId,
        string toParticipantId,
        long amountMinor,
        DateTime date)
    {
        var infra = await _getInfra();
        var selectedGroupId = await _getSelectedGroupId();

        await new AddManualTransferUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.TransferRepository,
            new GuidIdGenerator(),
            new UtcClock()).ExecuteAsync(new AddManualTransferInput(
                GroupId: selectedGroupId,
                FromParticipantId: fromParticipantId,
                ToParticipantId: toParticipantId,
                AmountMinor: amountMinor,
                Date: date.ToUniversalTime().ToString("O"),
                Note: "Recorded in app"));
    }
}
