namespace LuSplit.Domain.Shared;

public sealed class DomainInvariantException : Exception
{
    public DomainInvariantException(string message)
        : base(message)
    {
    }
}
