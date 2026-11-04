namespace IplStore.Tests.Support;

/// <summary>A clock the test moves by hand.</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
    }
}
