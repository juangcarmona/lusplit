namespace LuSplit.Application.Shared.Ports;

public interface IClock
{
    string NowIso();
    DateTimeOffset UtcNow { get; }
}
