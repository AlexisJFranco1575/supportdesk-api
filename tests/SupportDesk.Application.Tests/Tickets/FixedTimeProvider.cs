namespace SupportDesk.Application.Tests.Tickets;

/// <summary>A controllable clock so tests are deterministic.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}