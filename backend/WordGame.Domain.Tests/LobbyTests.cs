namespace WordGame.Domain.Tests;

public sealed class LobbyTests
{
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
