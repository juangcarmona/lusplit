using LuSplit.Functions.Functions;

namespace LuSplit.Functions.Tests;

public sealed class KeyFunctionsTests
{
    [Fact]
    public void UploadRotatedKey_Method_Exists()
    {
        var method = typeof(KeyFunctions).GetMethod(nameof(KeyFunctions.UploadRotatedKey));
        Assert.NotNull(method);
    }

    [Fact]
    public void GetWrappedKeysForDevice_Method_Exists()
    {
        var method = typeof(KeyFunctions).GetMethod(nameof(KeyFunctions.GetWrappedKeysForDevice));
        Assert.NotNull(method);
    }
}
