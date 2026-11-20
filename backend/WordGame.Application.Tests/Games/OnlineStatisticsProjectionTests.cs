using WordGame.Application.Statistics;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class OnlineStatisticsProjectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecordsCompletedBluffCallsFromCallerPerspectiveAndPreservesForfeitWinner(bool bidderSucceeded)
    {
        var game = await ChallengeTestGame.Create(players: 2, required: 1, settings: MatchSettings.Online());
        Assert.Null(OnlineMatchHistory.From(await game.State()));
        if (bidderSucceeded) { TurnTestGame.Accepted(await game.Submit(0, "Stockholm")); }
        else
        {
            game.Clock.Advance(TimeSpan.FromMinutes(1));
            TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        }
        var ended = TurnTestGame.Accepted(await game.Engine.ForfeitAsync(game.Id, game.Players[0].Id, await game.Context()));
        var record = OnlineMatchHistory.From(ended)!;
        var caller = Assert.Single(record.Players, player => player.PlayerId == game.Players[1].Id);
        Assert.True(caller.Won);
        Assert.Equal(bidderSucceeded ? 0 : 1, caller.SuccessfulBluffChallenges);
        Assert.Equal(bidderSucceeded ? 1 : 0, caller.FailedBluffChallenges);
        Assert.All(record.Players.Where(player => player.PlayerId != caller.PlayerId), player =>
        {
            Assert.Equal(0, player.SuccessfulBluffChallenges);
            Assert.Equal(0, player.FailedBluffChallenges);
        });
    }

    [Fact]
    public async Task PartyResultsAreExcluded()
    {
        var game = await ChallengeTestGame.Create(players: 2, required: 1, settings: MatchSettings.Party(1));
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        Assert.Equal(WordGame.Application.Sessions.SessionStatus.Completed, (await game.State()).Status);
        Assert.Null(OnlineMatchHistory.From(await game.State()));
    }
}
