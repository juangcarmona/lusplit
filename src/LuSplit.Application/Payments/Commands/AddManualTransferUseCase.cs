using System.Text.Json;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Payments.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Payments.Models;
using LuSplit.Application.Sync.Ports;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Payments;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Payments.Commands;

public sealed class AddManualTransferUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly IOperationRepository? _operationRepository;
    private readonly ISharedGroupStateRepository? _sharedGroupStateRepository;

    public AddManualTransferUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        ITransferRepository transferRepository,
        IIdGenerator idGenerator,
        IClock clock,
        IOperationRepository? operationRepository = null,
        ISharedGroupStateRepository? sharedGroupStateRepository = null)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _transferRepository = transferRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _operationRepository = operationRepository;
        _sharedGroupStateRepository = sharedGroupStateRepository;
    }

    public async Task<TransferModel> ExecuteAsync(AddManualTransferInput input, CancellationToken cancellationToken = default)
    {
        UseCaseGuards.AssertNonEmpty(input.GroupId, "groupId");
        UseCaseGuards.AssertNonEmpty(input.FromParticipantId, "fromParticipantId");
        UseCaseGuards.AssertNonEmpty(input.ToParticipantId, "toParticipantId");

        if (input.AmountMinor <= 0)
        {
            throw new ValidationError("amountMinor must be greater than zero");
        }

        if (string.Equals(input.FromParticipantId, input.ToParticipantId, StringComparison.Ordinal))
        {
            throw new ValidationError("fromParticipantId and toParticipantId must be different");
        }

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        if (group.Closed)
        {
            throw new ValidationError($"Group is closed: {group.Id}");
        }

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(input.GroupId, cancellationToken);
        var ids = participants.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (!ids.Contains(input.FromParticipantId) || !ids.Contains(input.ToParticipantId))
        {
            throw new ValidationError($"Transfer participants must belong to group {input.GroupId}");
        }

        var date = UseCaseGuards.ResolveDate(input.Date, _clock.NowIso());

        var transfer = new Transfer(
            _idGenerator.NextId(),
            input.GroupId,
            input.FromParticipantId,
            input.ToParticipantId,
            input.AmountMinor,
            date,
            TransferType.Manual,
            input.Note);

        await _transferRepository.SaveTransferAsync(transfer, cancellationToken);

        await EnqueueOperationIfSharedAsync(transfer, cancellationToken);

        return new TransferModel(
            transfer.Id,
            transfer.GroupId,
            transfer.FromParticipantId,
            transfer.ToParticipantId,
            transfer.AmountMinor,
            transfer.Date,
            transfer.Type == TransferType.Manual ? "MANUAL" : "GENERATED",
            transfer.Note);
    }

    private async Task EnqueueOperationIfSharedAsync(Transfer transfer, CancellationToken ct)
    {
        if (_operationRepository is null || _sharedGroupStateRepository is null) return;
        var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(transfer.GroupId, ct);
        if (sharedState is null || !sharedState.IsShared) return;

        var payload = new AddTransferPayload(
            transfer.Id, transfer.FromParticipantId, transfer.ToParticipantId,
            transfer.AmountMinor / 100m, _clock.UtcNow);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var operation = new Operation(
            _idGenerator.NextId(), transfer.GroupId, "", "",
            _clock.UtcNow.Ticks.ToString("D20"),
            OperationType.AddTransfer, transfer.Id,
            payloadBytes, sharedState.CurrentKeyVersion, _clock.UtcNow);

        await _operationRepository.SaveAsync(operation, ct);
    }
}
