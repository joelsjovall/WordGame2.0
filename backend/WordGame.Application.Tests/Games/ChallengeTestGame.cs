using WordGame.Application.Challenges;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

internal static class ChallengeTestGame
{
    internal static CategoryDefinition Capitals() => new(new Category("capitals", "European Capitals", CategoryDifficulty.Easy),
    [
        new AcceptedAnswer("Stockholm"), new AcceptedAnswer("Paris"), new AcceptedAnswer("Berlin"),
        new AcceptedAnswer("Madrid"), new AcceptedAnswer("Rome"), new AcceptedAnswer("Copenhagen", ["Köpenhamn"])
    ]);

    internal static async Task<TurnTestGame> Create(int players = 2, int required = 5, TimeSpan? duration = null, TestCategoryProvider? provider = null)
    {
        var game = await TurnTestGame.Create(players, provider ?? new TestCategoryProvider(Capitals()),
            challengeRules: new ChallengeRules(duration ?? TurnTestGame.Duration));
        TurnTestGame.Accepted(await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "capitals", await game.Context()));
        await game.Bid(0, required);
        await game.Bluff(1);
        return game;
    }
}
