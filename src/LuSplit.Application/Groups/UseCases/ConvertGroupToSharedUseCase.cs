using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.UseCases;

public sealed class ConvertGroupToSharedUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupRegistrationPort _registrationPort;
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly IAuthPort _authPort;
    private readonly IIdGenerator _idGenerator;

    public ConvertGroupToSharedUseCase(
        IGroupRepository groupRepository,
        IGroupRegistrationPort registrationPort,
        ISharedGroupStateRepository sharedStateRepository,
        IAuthPort authPort,
        IIdGenerator idGenerator)
    {
        _groupRepository = groupRepository;
        _registrationPort = registrationPort;
        _sharedStateRepository = sharedStateRepository;
        _authPort = authPort;
        _idGenerator = idGenerator;
    }

    public async Task ExecuteAsync(string groupId, string deviceId, CancellationToken ct)
    {
        var userId = await _authPort.GetCurrentUserIdAsync(ct)
            ?? throw new ValidationError("User must be signed in to convert a group.");

        var group = await _groupRepository.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundError($"Group {groupId} not found.");

        var existingState = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);
        if (existingState?.IsShared == true)
            throw new ValidationError("Group is already shared.");

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var groupKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        const int initialKeyVersion = 1;
        var wrappedKey = rsa.Encrypt(groupKey, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA256);

        var request = new CreateGroupRequest(
            GroupId: groupId,
            OwnerId: userId,
            OwnerDeviceId: deviceId,
            InitialKeyVersion: initialKeyVersion,
            WrappedKeys: [new WrappedKeyEntryDto(deviceId, wrappedKey)]);

        var response = await _registrationPort.RegisterGroupAsync(request, ct);

        var sharedState = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: response.ContainerName,
            OwnerId: userId,
            CurrentKeyVersion: initialKeyVersion,
            SyncStatus: SyncStatus.PendingLocalChanges,
            IsReadOnly: false);

        await _sharedStateRepository.SaveAsync(groupId, sharedState, ct);
    }
}
