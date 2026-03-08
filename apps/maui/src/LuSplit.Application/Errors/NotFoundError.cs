namespace LuSplit.Application.Errors;

public sealed class NotFoundError : Exception
{
    public NotFoundError(string message)
        : base(message)
    {
    }
}
