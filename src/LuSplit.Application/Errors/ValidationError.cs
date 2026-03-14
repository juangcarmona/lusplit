namespace LuSplit.Application.Errors;

public sealed class ValidationError : Exception
{
    public ValidationError(string message)
        : base(message)
    {
    }
}
