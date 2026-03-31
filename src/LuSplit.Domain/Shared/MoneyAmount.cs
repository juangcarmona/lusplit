using LuSplit.Domain.Shared;

namespace LuSplit.Domain.Shared;

public readonly record struct MoneyAmount
{
    public long MinorUnits { get; }

    private MoneyAmount(long minorUnits)
    {
        MinorUnits = minorUnits;
    }

    public static MoneyAmount FromMinorUnits(long minorUnits)
    {
        if (minorUnits < 0)
        {
            throw new DomainInvariantException("Money must be represented as non-negative minor units.");
        }

        return new MoneyAmount(minorUnits);
    }

    public static MoneyAmount FromMinorUnitsDecimal(decimal minorUnits)
    {
        if (minorUnits != decimal.Truncate(minorUnits))
        {
            throw new DomainInvariantException("Money minor units must be integers.");
        }

        return FromMinorUnits((long)minorUnits);
    }
}
