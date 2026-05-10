using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class HealthFunctionsTests
{
    [Fact]
    public void HealthCheck_Method_Exists()
    {
        var method = typeof(HealthFunctions).GetMethod(nameof(HealthFunctions.HealthCheck));
        Assert.NotNull(method);
        Assert.True(method.ReturnType == typeof(Microsoft.Azure.Functions.Worker.Http.HttpResponseData));
    }
}
