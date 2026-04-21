using LuSplit.Application.Identity.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.App.Features.Devices;
using LuSplit.Contracts.ControlPlane;
using NSubstitute;

namespace LuSplit.App.Tests;

public sealed class DeviceManagementViewModelTests
{
    private const string UserId = "user-1";

    private readonly IDeviceRegistrationPort _registrationPort = Substitute.For<IDeviceRegistrationPort>();
    private readonly IAuthPort _authPort = Substitute.For<IAuthPort>();

    private DeviceManagementViewModel CreateSut() => new(_registrationPort, _authPort);

    private static IReadOnlyList<DeviceDto> SampleDevices() =>
    [
        new DeviceDto("dev-1", "My Phone", "Android", DateTimeOffset.UtcNow, false),
        new DeviceDto("dev-2", "My Tablet", "iOS", DateTimeOffset.UtcNow, false)
    ];

    [Fact]
    public async Task LoadCommand_PopulatesDevices()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _registrationPort.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new ListDevicesResponse(SampleDevices()));

        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Devices.Count);
    }

    [Fact]
    public async Task LoadCommand_SetsIsLoadingFalseAfterSuccess()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _registrationPort.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new ListDevicesResponse(SampleDevices()));

        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadCommand_OnFailure_SetsErrorMessage()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _registrationPort.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns<ListDevicesResponse>(_ => throw new HttpRequestException("Connection refused"));

        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task RevokeDeviceCommand_RemovesDeviceFromList()
    {
        var devices = SampleDevices().ToList();
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _registrationPort.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new ListDevicesResponse(devices));
        _registrationPort.RevokeDeviceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        var deviceToRevoke = vm.Devices.First();
        await vm.RevokeDeviceCommand.ExecuteAsync(deviceToRevoke);

        Assert.DoesNotContain(vm.Devices, d => d.DeviceId == deviceToRevoke.DeviceId);
    }

    [Fact]
    public async Task RevokeDeviceCommand_CallsPortWithDeviceIdAndUserId()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _registrationPort.RevokeDeviceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var vm = CreateSut();
        var device = SampleDevices()[0];
        await vm.RevokeDeviceCommand.ExecuteAsync(device);

        await _registrationPort.Received(1).RevokeDeviceAsync(device.DeviceId, UserId, Arg.Any<CancellationToken>());
    }
}
