using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;

namespace LuSplit.Application.Commands;

public sealed class CloseGroupUseCase
{
    private readonly IGroupRepository _groupRepository;

    public CloseGroupUseCase(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GroupModel> ExecuteAsync(CloseGroupInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.GroupId))
        {
            throw new ValidationError("groupId is required");
        }

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        var closedGroup = group with
        {
            Closed = true
        };

        await _groupRepository.SaveGroupAsync(closedGroup, cancellationToken);

        return new GroupModel(closedGroup.Id, closedGroup.Currency, closedGroup.Closed);
    }
}
