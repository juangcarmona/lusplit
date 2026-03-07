using LuSplit.Application.Ports;

namespace LuSplit.Application.Tests.Fakes;

internal sealed class SequentialIdGenerator : IIdGenerator
{
    private int _current;

    public string NextId()
    {
        _current += 1;
        return $"id-{_current}";
    }
}

internal sealed class FixedClock : IClock
{
    private readonly string _nowIso;

    public FixedClock(string nowIso)
    {
        _nowIso = nowIso;
    }

    public string NowIso() => _nowIso;
}
