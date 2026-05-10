using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class GroupFunctionsTests
{
    [Fact]
    public void CreateGroup_Method_Exists()
    {
        var method = typeof(GroupFunctions).GetMethod(nameof(GroupFunctions.CreateGroup));
        Assert.NotNull(method);
        Assert.True(method.ReturnType == typeof(Task<Microsoft.Azure.Functions.Worker.Http.HttpResponseData>));
    }

    [Fact]
    public void GetGroupInfo_Method_Exists()
    {
        var method = typeof(GroupFunctions).GetMethod(nameof(GroupFunctions.GetGroupInfo));
        Assert.NotNull(method);
        Assert.True(method.ReturnType == typeof(Task<Microsoft.Azure.Functions.Worker.Http.HttpResponseData>));
    }
}
