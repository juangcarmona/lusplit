using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class MemberFunctionsTests
{
    [Fact]
    public void RevokeMember_Method_Exists()
    {
        var method = typeof(MemberFunctions).GetMethod(nameof(MemberFunctions.RevokeMember));
        Assert.NotNull(method);
    }

    [Fact]
    public void TransferOwnership_Method_Exists()
    {
        var method = typeof(MemberFunctions).GetMethod(nameof(MemberFunctions.TransferOwnership));
        Assert.NotNull(method);
    }
}
