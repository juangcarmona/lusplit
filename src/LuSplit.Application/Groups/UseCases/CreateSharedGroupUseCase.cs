using System.Security.Cryptography;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.UseCases;

public sealed class CreateSharedGroupUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupRegistrationPort _registrationPort;
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly IGroupMembershipRepository _membershipRepository;
    private readonly ISecureKeyStoragePort _keyStorage;
    private readonly IAuthPort _authPort;
    private readonly IIdGenerator _idGenerator;

    public CreateSharedGroupUseCase(
        IGroupRepository groupRepository,
        IGroupRegistrationPort registrationPort,
        ISharedGroupStateRepository sharedStateRepository,
        IGroupMembershipRepository membershipRepository,
        ISecureKeyStoragePort keyStorage,
        IAuthPort authPort,
        IIdGenerator idGenerator)
    {
        _groupRepository = groupRepository;
        _registrationPort = registrationPort;
        _sharedStateRepository = sharedStateRepository;
        _membershipRepository = membershipRepository;
        _keyStorage = keyStorage;
        _authPort = authPort;
        _idGenerator = idGenerator;
    }

    public async Task<string> ExecuteAsync(string currency, string deviceId, CancellationToken ct)
    {
        var userId = await _authPort.GetCurrentUserIdAsync(ct)
            ?? throw new ValidationError("User must be signed in to create a shared group.");

        // Generate new group key
        var groupKey = RandomNumberGenerator.GetBytes(32);
        const int initialKeyVersion = 1;

        // Generate RSA device keypair; wrap group key with device public key
        using var rsa = RSA.Create(2048);
        var publicKeyBytes = rsa.ExportRSAPublicKey();
        var wrappedKey = rsa.Encrypt(groupKey, RSAEncryptionPadding.OaepSHA256);
        var privateKeyBytes = rsa.ExportRSAPrivateKey();

        var groupId = _idGenerator.NextId();

        var request = new CreateGroupRequest(
            GroupId: groupId,
            OwnerId: userId,
            OwnerDeviceId: deviceId,
            InitialKeyVersion: initialKeyVersion,
            WrappedKeys: [new WrappedKeyEntryDto(deviceId, wrappedKey)]);

        var response = await _registrationPort.RegisterGroupAsync(request, ct);

        var group = new Group(response.GroupId, currency, false);
        await _groupRepository.SaveGroupAsync(group, ct);

        var sharedState = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: response.ContainerName,
            OwnerId: userId,
            CurrentKeyVersion: initialKeyVersion,
            SyncStatus: SyncStatus.UpToDate,
            IsReadOnly: false);

        await _sharedStateRepository.SaveAsync(response.GroupId, sharedState, ct);

        await _keyStorage.StoreWrappedKeyAsync(response.GroupId, initialKeyVersion, wrappedKey, ct);
        await _keyStorage.StorePrivateKeyAsync(deviceId, privateKeyBytes, ct);

        // Seed local owner membership so role-aware UI works before next sync
        var ownerMembership = new GroupMembership(
            response.GroupId, userId, MemberRole.Owner, DateTimeOffset.UtcNow, false, null);
        await _membershipRepository.UpsertAsync(ownerMembership, ct);

        return response.GroupId;
    }
}
