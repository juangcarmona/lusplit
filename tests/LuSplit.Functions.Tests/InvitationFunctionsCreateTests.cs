using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class InvitationFunctionsCreateTests
{
    [Fact]
    public void CreateInvitation_Method_Exists()
    {
        var method = typeof(InvitationFunctions).GetMethod(nameof(InvitationFunctions.CreateInvitation));
        Assert.NotNull(method);
    }
}
