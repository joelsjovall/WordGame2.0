using WordGame.Infrastructure.Sessions;

namespace WordGame.Infrastructure.Tests;

public sealed class GuestSessionStoreTests
{
    [Fact]
    public void SessionsHaveIndependentCredentialsAbsoluteExpiryAndRevocation()
    {
        var clock = new Clock();
        using var store = new InMemoryGuestSessionStore(clock);
        var first = store.Issue(Guid.NewGuid());
        var second = store.Issue(Guid.NewGuid());
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.NotEqual(first.Id, first.PlayerId);
        Assert.Equal(TimeSpan.FromDays(1), first.ExpiresAt - first.IssuedAt);
        clock.Now = clock.Now.AddHours(23);
        Assert.Equal(first.ExpiresAt, store.Get(first.Id)!.ExpiresAt);
        store.Revoke(first.Id);
        Assert.Null(store.Get(first.Id));
        Assert.True(first.Revoked.IsCancellationRequested);
        Assert.NotNull(store.Get(second.Id));
        clock.Now = clock.Now.AddHours(1);
        Assert.Null(store.Get(second.Id));
        Assert.True(second.Revoked.IsCancellationRequested);
        Assert.Throws<ArgumentException>(() => store.Issue(Guid.Empty));
    }

    [Fact]
    public void CreatingSessionsPrunesExpiredEntriesAndDisposalRevokesActiveStreams()
    {
        var clock = new Clock();
        var store = new InMemoryGuestSessionStore(clock);
        var expired = store.Issue(Guid.NewGuid());
        clock.Now = clock.Now.AddDays(1);
        var active = store.Issue(Guid.NewGuid());
        Assert.True(expired.Revoked.IsCancellationRequested);
        store.Dispose();
        Assert.True(active.Revoked.IsCancellationRequested);
        Assert.Null(store.Get(active.Id));
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
