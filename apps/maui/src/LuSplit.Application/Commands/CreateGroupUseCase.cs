using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Commands;

public sealed class CreateGroupUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IIdGenerator _idGenerator;

    public CreateGroupUseCase(IGroupRepository groupRepository, IIdGenerator idGenerator)
    {
        _groupRepository = groupRepository;
        _idGenerator = idGenerator;
    }

    public async Task<GroupModel> ExecuteAsync(CreateGroupInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Currency))
        {
            throw new ValidationError("currency is required");
        }

        var group = new Group(_idGenerator.NextId(), input.Currency, false);
        await _groupRepository.SaveGroupAsync(group, cancellationToken);

        return new GroupModel(group.Id, group.Currency, group.Closed);
    }
}
