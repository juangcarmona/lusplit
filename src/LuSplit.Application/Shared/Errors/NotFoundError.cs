namespace LuSplit.Application.Shared.Errors;

public sealed class NotFoundError : Exception
{
    public NotFoundError(string message)
        : base(message)
    {
    }
}
