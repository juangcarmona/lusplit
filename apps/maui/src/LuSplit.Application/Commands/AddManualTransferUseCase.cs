using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Commands;

public sealed class AddManualTransferUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public AddManualTransferUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        ITransferRepository transferRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _transferRepository = transferRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<TransferModel> ExecuteAsync(AddManualTransferInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.GroupId))
        {
            throw new ValidationError("groupId is required");
        }

        if (string.IsNullOrWhiteSpace(input.FromParticipantId))
        {
            throw new ValidationError("fromParticipantId is required");
        }

        if (string.IsNullOrWhiteSpace(input.ToParticipantId))
        {
            throw new ValidationError("toParticipantId is required");
        }

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

        var date = input.Date ?? _clock.NowIso();
        if (!DateTimeOffset.TryParse(date, out _))
        {
            throw new ValidationError("date must be a valid ISO date");
        }

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
}
