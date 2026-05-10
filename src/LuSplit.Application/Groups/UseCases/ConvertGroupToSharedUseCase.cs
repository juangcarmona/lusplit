using System.Security.Cryptography;
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
    private readonly ISecureKeyStoragePort _keyStorage;
    private readonly IAuthPort _authPort;

    public ConvertGroupToSharedUseCase(
        IGroupRepository groupRepository,
        IGroupRegistrationPort registrationPort,
        ISharedGroupStateRepository sharedStateRepository,
        ISecureKeyStoragePort keyStorage,
        IAuthPort authPort)
    {
        _groupRepository = groupRepository;
        _registrationPort = registrationPort;
        _sharedStateRepository = sharedStateRepository;
        _keyStorage = keyStorage;
        _authPort = authPort;
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

        using var rsa = RSA.Create(2048);
        var groupKey = RandomNumberGenerator.GetBytes(32);
        const int initialKeyVersion = 1;
        var wrappedKey = rsa.Encrypt(groupKey, RSAEncryptionPadding.OaepSHA256);
        var privateKeyBytes = rsa.ExportRSAPrivateKey();

        var request = new CreateGroupRequest(
            GroupId: groupId,
            OwnerId: userId,
            OwnerDeviceId: deviceId,
            InitialKeyVersion: initialKeyVersion,
            WrappedKeys: [new WrappedKeyEntryDto(deviceId, wrappedKey)]);

        string containerName;
        try
        {
            var response = await _registrationPort.RegisterGroupAsync(request, ct);
            containerName = response.ContainerName;
        }
        catch (InvalidOperationException) when (existingState is null)
        {
            // 409 Conflict — group already registered on a previous attempt that
            // failed locally. Fetch the existing registration and proceed.
            var info = await _registrationPort.GetGroupInfoAsync(groupId, ct);
            if (!string.Equals(info.OwnerId, userId, StringComparison.OrdinalIgnoreCase))
                throw;
            containerName = $"grp-{groupId.ToLowerInvariant().Replace("-", "")}";
        }

        var sharedState = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: containerName,
            OwnerId: userId,
            CurrentKeyVersion: initialKeyVersion,
            SyncStatus: SyncStatus.PendingLocalChanges,
            IsReadOnly: false);

        await _sharedStateRepository.SaveAsync(groupId, sharedState, ct);

        await _keyStorage.StoreWrappedKeyAsync(groupId, initialKeyVersion, wrappedKey, ct);
        await _keyStorage.StorePrivateKeyAsync(deviceId, privateKeyBytes, ct);
    }
}
