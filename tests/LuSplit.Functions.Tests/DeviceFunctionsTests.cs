using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class DeviceFunctionsTests
{
    [Fact]
    public void RegisterDevice_Method_Exists()
    {
        var method = typeof(DeviceFunctions).GetMethod(nameof(DeviceFunctions.RegisterDevice));
        Assert.NotNull(method);
    }

    [Fact]
    public void ListDevices_Method_Exists()
    {
        var method = typeof(DeviceFunctions).GetMethod(nameof(DeviceFunctions.ListDevices));
        Assert.NotNull(method);
    }

    [Fact]
    public void RevokeDevice_Method_Exists()
    {
        var method = typeof(DeviceFunctions).GetMethod(nameof(DeviceFunctions.RevokeDevice));
        Assert.NotNull(method);
    }
}
