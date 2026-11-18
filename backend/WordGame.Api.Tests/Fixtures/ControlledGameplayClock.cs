namespace WordGame.Api.Tests.Fixtures;

internal sealed class ControlledGameplayClock : TimeProvider
{
    private long ticks = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero).Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    internal void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
}
