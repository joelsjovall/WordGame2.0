namespace WordGame.Domain.Tests;

public sealed class LobbyTests
{
    [Fact]
    public void JoiningPreservesSnapshotsAndRejectsDuplicateIdentitiesNamesAndOverCapacity()
    {
        var host = new Player(Guid.NewGuid(), "Alice");
        var original = new Lobby(Guid.NewGuid(), new LobbyCode("ABC234"), host, DomainTestData.StartedAt, DomainTestData.StartedAt.AddMinutes(30));
        var bob = new Player(Guid.NewGuid(), "Bob");
        var joined = original.Join(bob);
        Assert.Single(original.Players);
        Assert.Equal(2, joined.Players.Count);
        Assert.Equal(original.Id, joined.Id);
        Assert.Equal(original.Code, joined.Code);
        Assert.Equal(original.HostPlayerId, joined.HostPlayerId);
        Assert.Equal(original.ExpiresAt, joined.ExpiresAt);
        Assert.Same(joined, joined.Join(new Player(bob.Id, " bob ")));
        Assert.Equal("Bob", joined.Players[1].DisplayName);
        Assert.Equal(LobbyJoinFailure.DifferentPlayerName, Assert.Throws<LobbyJoinException>(() => joined.Join(new Player(bob.Id, "Changed"))).Reason);
        Assert.Equal(LobbyJoinFailure.DisplayNameTaken, Assert.Throws<LobbyJoinException>(() => joined.Join(new Player(Guid.NewGuid(), "ALICE"))).Reason);
        var full = joined.Join(new Player(Guid.NewGuid(), "Carol")).Join(new Player(Guid.NewGuid(), "Dan"));
        Assert.Equal(4, full.Players.Count);
        Assert.Same(full, full.Join(bob));
        Assert.Equal(LobbyJoinFailure.Full, Assert.Throws<LobbyJoinException>(() => full.Join(new Player(Guid.NewGuid(), "Eve"))).Reason);
        Assert.Throws<ArgumentException>(() => original.Join(new Player(Guid.NewGuid(), new string('A', 33))));
        Assert.Throws<ArgumentNullException>(() => original.Join(null!));
    }

    [Fact]
    public void LobbyStartsWithOneHostAndUsesTheSharedGameplayCapacity()
    {
        var host = new Player(Guid.NewGuid(), " Alice ");
        var now = DomainTestData.StartedAt;
        var lobby = new Lobby(Guid.NewGuid(), new LobbyCode(" abC234 "), host, now, now.AddMinutes(30));
        Assert.Equal("ABC234", lobby.Code.Value);
        Assert.Equal(host.Id, lobby.HostPlayerId);
        Assert.Same(host, Assert.Single(lobby.Players));
        Assert.Equal("Alice", lobby.Players[0].DisplayName);
        Assert.Equal(LobbyStatus.Waiting, lobby.Status);
        Assert.Equal(Game.MinimumPlayers, Lobby.MinimumPlayers);
        Assert.Equal(Game.MaximumPlayers, Lobby.MaximumPlayers);
        Assert.Throws<NotSupportedException>(() => ((IList<Player>)lobby.Players).Clear());
        Assert.Throws<ArgumentException>(() => new Game(Guid.NewGuid(), lobby.Players));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("ABCI23")]
    [InlineData("ABCO23")]
    [InlineData("ABC023")]
    [InlineData("ABC123")]
    [InlineData("ABC-23")]
    public void CodesRejectAmbiguousOrMalformedText(string code) =>
        Assert.ThrowsAny<ArgumentException>(() => new LobbyCode(code));

    [Fact]
    public void LobbyRejectsInvalidIdentityNameAndExpiration()
    {
        var host = new Player(Guid.NewGuid(), "Alice");
        var now = DomainTestData.StartedAt;
        var code = new LobbyCode("ABC234");
        Assert.Throws<ArgumentException>(() => new Lobby(Guid.Empty, code, host, now, now.AddMinutes(30)));
        Assert.Throws<ArgumentNullException>(() => new Lobby(Guid.NewGuid(), null!, host, now, now.AddMinutes(30)));
        Assert.Throws<ArgumentNullException>(() => new Lobby(Guid.NewGuid(), code, null!, now, now.AddMinutes(30)));
        Assert.Throws<ArgumentException>(() => new Lobby(Guid.NewGuid(), code, new Player(host.Id, new string('A', 33)), now, now.AddMinutes(30)));
        Assert.Throws<ArgumentException>(() => new Lobby(Guid.NewGuid(), code, new Player(host.Id, "Ali\nce"), now, now.AddMinutes(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lobby(Guid.NewGuid(), code, host, now, now));
    }
}
