namespace WordGame.Api.Tests.Fixtures;

internal sealed class ControlledGameplayClock : TimeProvider
{
    // Start near real time so HttpClient's cookie jar does not discard cookies as already expired.
    // Time remains frozen until Advance; gameplay assertions depend on elapsed time, not a calendar date.
    private long ticks = DateTimeOffset.UtcNow.Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    internal void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
}
