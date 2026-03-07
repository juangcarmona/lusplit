using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface IParticipantRepository
{
    Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveParticipantAsync(Participant participant, CancellationToken cancellationToken);
}
