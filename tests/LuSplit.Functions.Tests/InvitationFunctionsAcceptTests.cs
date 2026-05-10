using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class InvitationFunctionsAcceptTests
{
    [Fact]
    public void AcceptInvitation_Method_Exists()
    {
        var method = typeof(InvitationFunctions).GetMethod(nameof(InvitationFunctions.AcceptInvitation));
        Assert.NotNull(method);
    }

    [Fact]
    public void DeclineInvitation_Method_Exists()
    {
        var method = typeof(InvitationFunctions).GetMethod(nameof(InvitationFunctions.DeclineInvitation));
        Assert.NotNull(method);
    }
}
