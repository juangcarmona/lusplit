using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Ports;

public interface IParticipantRepository
{
    Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task SaveParticipantAsync(Participant participant, CancellationToken cancellationToken);
}
