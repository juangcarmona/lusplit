namespace LuSplit.Domain.Expenses;

public enum RemainderMode
{
    Equal,
    Weight,
    Percent
}

public abstract record SplitComponent;

public sealed record FixedSplitComponent(IReadOnlyDictionary<string, long> Shares) : SplitComponent;

public sealed record RemainderSplitComponent(
    IReadOnlyList<string> Participants,
    RemainderMode Mode,
    IReadOnlyDictionary<string, string>? Weights = null,
    IReadOnlyDictionary<string, int>? Percents = null) : SplitComponent;

public sealed record SplitDefinition(IReadOnlyList<SplitComponent> Components);
