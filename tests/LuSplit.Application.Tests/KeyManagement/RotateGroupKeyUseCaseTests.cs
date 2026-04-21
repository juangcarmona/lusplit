using LuSplit.Application.Groups.Ports;
using LuSplit.Application.KeyManagement.Ports;
using LuSplit.Application.KeyManagement.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.KeyManagement;

public sealed class RotateGroupKeyUseCaseTests
{
    private readonly IKeyRotationPort _keyRotationPort = Substitute.For<IKeyRotationPort>();
    private readonly ISharedGroupStateRepository _sharedStateRepository = Substitute.For<ISharedGroupStateRepository>();
    private readonly IKeyWrapPort _keyWrapPort = Substitute.For<IKeyWrapPort>();
    private readonly IEncryptionPort _encryption = Substitute.For<IEncryptionPort>();

    private RotateGroupKeyUseCase CreateSut() =>
        new(_keyRotationPort, _sharedStateRepository, _keyWrapPort, _encryption);

    [Fact]
    public async Task ExecuteAsync_RotatesKey_NewVersionIsHigherThanCurrent()
    {
        const string GroupId = "group-1";
        var sharedState = new SharedGroupState(true, "container1", "owner1", 2, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);
        _keyRotationPort.GetDevicePublicKeysAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new List<DevicePublicKeyDto> { new("dev-1", new byte[32]) } as IReadOnlyList<DevicePublicKeyDto>);
        _keyWrapPort.WrapKey(Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(new byte[256]);

        await CreateSut().ExecuteAsync(GroupId);

        await _keyRotationPort.Received(1).UploadRotatedKeyAsync(
            GroupId,
            Arg.Is<UploadRotatedKeyRequest>(r => r.NewKeyVersion == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WrapsKeyForEachDevice()
    {
        const string GroupId = "group-1";
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);
        _keyRotationPort.GetDevicePublicKeysAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new List<DevicePublicKeyDto>
            {
                new("dev-1", new byte[32]),
                new("dev-2", new byte[32])
            } as IReadOnlyList<DevicePublicKeyDto>);
        _keyWrapPort.WrapKey(Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(new byte[256]);

        await CreateSut().ExecuteAsync(GroupId);

        await _keyRotationPort.Received(1).UploadRotatedKeyAsync(
            GroupId,
            Arg.Is<UploadRotatedKeyRequest>(r => r.WrappedKeys.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RevokedDeviceAbsent_WrapsOnlyActiveDevices()
    {
        const string GroupId = "group-1";
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);
        // GetDevicePublicKeysAsync returns only non-revoked devices
        _keyRotationPort.GetDevicePublicKeysAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new List<DevicePublicKeyDto> { new("dev-remaining", new byte[32]) } as IReadOnlyList<DevicePublicKeyDto>);
        _keyWrapPort.WrapKey(Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(new byte[256]);

        await CreateSut().ExecuteAsync(GroupId);

        await _keyRotationPort.Received(1).UploadRotatedKeyAsync(
            GroupId,
            Arg.Is<UploadRotatedKeyRequest>(r =>
                r.WrappedKeys.Count == 1 &&
                r.WrappedKeys[0].DeviceId == "dev-remaining"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesLocalKeyVersion()
    {
        const string GroupId = "group-1";
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);
        _keyRotationPort.GetDevicePublicKeysAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new List<DevicePublicKeyDto> { new("dev-1", new byte[32]) } as IReadOnlyList<DevicePublicKeyDto>);
        _keyWrapPort.WrapKey(Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(new byte[256]);

        await CreateSut().ExecuteAsync(GroupId);

        await _sharedStateRepository.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.CurrentKeyVersion == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_Throws()
    {
        _sharedStateRepository.GetByGroupIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ExecuteAsync("group-x"));
    }
}
