namespace LuSplit.Domain.Errors;

public sealed class DomainInvariantException : Exception
{
    public DomainInvariantException(string message)
        : base(message)
    {
    }
}
