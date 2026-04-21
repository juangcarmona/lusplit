using LuSplit.Application.Identity.Ports;
using LuSplit.Application.Identity.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using NSubstitute;

namespace LuSplit.Application.Tests.Identity;

public sealed class RegisterDeviceUseCaseTests
{
    private readonly IDeviceRegistrationPort _registrationPort = Substitute.For<IDeviceRegistrationPort>();
    private readonly ISecureKeyStoragePort _keyStorage = Substitute.For<ISecureKeyStoragePort>();

    private RegisterDeviceUseCase CreateSut() => new(_registrationPort, _keyStorage);

    [Fact]
    public async Task ExecuteAsync_ReturnsDeviceIdFromControlPlane()
    {
        _registrationPort.RegisterDeviceAsync(Arg.Any<RegisterDeviceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RegisterDeviceResponse("device-123"));

        var result = await CreateSut().ExecuteAsync("My Phone", "Android");

        Assert.Equal("device-123", result.DeviceId);
    }

    [Fact]
    public async Task ExecuteAsync_StoresPrivateKeyBeforeRegistering()
    {
        var callOrder = new List<string>();

        _keyStorage.StorePrivateKeyAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("store");
                return Task.CompletedTask;
            });

        _registrationPort.RegisterDeviceAsync(Arg.Any<RegisterDeviceRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("register");
                return Task.FromResult(new RegisterDeviceResponse("device-x"));
            });

        await CreateSut().ExecuteAsync("My Phone", "iOS");

        Assert.Equal(new[] { "store", "register" }, callOrder);
    }

    [Fact]
    public async Task ExecuteAsync_PostsPublicKeyToControlPlane()
    {
        byte[]? capturedPublicKey = null;

        _registrationPort.RegisterDeviceAsync(
                Arg.Do<RegisterDeviceRequest>(r => capturedPublicKey = r.PublicKey),
                Arg.Any<CancellationToken>())
            .Returns(new RegisterDeviceResponse("device-x"));

        var result = await CreateSut().ExecuteAsync("My Phone", "Android");

        Assert.NotNull(capturedPublicKey);
        Assert.NotEmpty(capturedPublicKey!);
        // Public key returned in result should match what was sent
        Assert.Equal(capturedPublicKey, result.PublicKey);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesUniqueDeviceIdsPerCall()
    {
        _registrationPort.RegisterDeviceAsync(Arg.Any<RegisterDeviceRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new RegisterDeviceResponse(ci.Arg<RegisterDeviceRequest>().DeviceId)));

        var sut = CreateSut();
        var result1 = await sut.ExecuteAsync("Phone 1", "Android");
        var result2 = await sut.ExecuteAsync("Phone 2", "Android");

        Assert.NotEqual(result1.DeviceId, result2.DeviceId);
    }
}
