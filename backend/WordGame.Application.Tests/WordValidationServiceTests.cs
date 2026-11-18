using System.Globalization;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Application.Tests;

public sealed class WordValidationServiceTests
{
    private readonly IWordValidationService service = new WordValidationService();
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Copenhagen")]
    [InlineData(" COPENHAGEN ")]
    [InlineData("Köpenhamn")]
    [InlineData(" KÖPENHAMN ")]
    [InlineData("Kobenhavn")]
    public void CanonicalValueAndAlternativeSpellingsAreAccepted(string word)
    {
        var result = service.Validate(Capitals(), word, []);

        Assert.Equal(WordSubmissionStatus.Accepted, result.Status);
        Assert.Equal("capital:copenhagen", result.CanonicalAnswerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    [InlineData("Copenhagn")]
    [InlineData("Copenhagen!")]
    [InlineData("Kopenhamn")]
    public void EmptyOrUnrecognizedAnswersAreRejectedWithoutGuessing(string? word)
    {
        var result = service.Validate(Capitals(), word, []);

        Assert.Equal(WordSubmissionStatus.Rejected, result.Status);
        Assert.Null(result.CanonicalAnswerId);
    }

    [Fact]
    public void WhitespaceRulesAreIdenticalForValuesAliasesAndSubmissions()
    {
        var category = new CategoryDefinition(
            new Category("cities", "Cities", CategoryDifficulty.Medium),
            [new AcceptedAnswer(" New  York ", [" Big\tApple "], "city:new-york")]);

        var value = service.Validate(category, " NEW\t\r\nYORK ", []);
        var alias = service.Validate(category, "BIG\u00a0  APPLE", []);
        Assert.Equal(WordSubmissionStatus.Accepted, value.Status);
        Assert.Equal(WordSubmissionStatus.Accepted, alias.Status);
        Assert.Equal("new york", value.NormalizedText);
        Assert.Equal("big apple", alias.NormalizedText);
        Assert.Equal(value.CanonicalAnswerId, alias.CanonicalAnswerId);
    }

    [Fact]
    public void CaseMatchingHandlesGreekFinalSigmaWithoutChangingAnswerIdentity()
    {
        var category = new CategoryDefinition(
            new Category("letters", "Letters", CategoryDifficulty.Hard),
            [new AcceptedAnswer("ΟΣ", id: "greek-answer")]);

        var result = service.Validate(category, "ος", []);

        Assert.Equal(WordSubmissionStatus.Accepted, result.Status);
        Assert.Equal("greek-answer", result.CanonicalAnswerId);
    }

    [Theory]
    [InlineData("Copenhagen", "COPENHAGEN")]
    [InlineData("Copenhagen", "Köpenhamn")]
    [InlineData("Köpenhamn", "Kobenhavn")]
    public void SpellingVariantsCannotCountAgainAcrossSubmissionCalls(string first, string second)
    {
        var category = Capitals();
        var challenge = CreateChallenge();
        var accepted = service.Validate(category, first, challenge.Submissions);
        challenge.RecordSubmission(ToSubmission(challenge, first, accepted));
        var duplicate = service.Validate(category, second, challenge.Submissions);
        challenge.RecordSubmission(ToSubmission(challenge, second, duplicate));

        Assert.Equal(WordSubmissionStatus.Duplicate, duplicate.Status);
        Assert.Equal(accepted.CanonicalAnswerId, duplicate.CanonicalAnswerId);
        Assert.Equal(1, challenge.AcceptedWordCount);
        Assert.Equal(2, challenge.Submissions.Count);
        // The domain remains a second guard if an adapter replays an Accepted result.
        Assert.Throws<InvalidOperationException>(() =>
            challenge.RecordSubmission(ToSubmission(challenge, first, accepted)));
    }

    [Fact]
    public void RejectedOrDuplicateHistoryDoesNotConsumeAnAnswer()
    {
        var challenge = CreateChallenge();
        var history = new[]
        {
            new SubmittedWord(challenge.Bid.PlayerId, "Copenhagen", StartedAt,
                WordSubmissionStatus.Rejected, "capital:copenhagen"),
            new SubmittedWord(challenge.Bid.PlayerId, "Köpenhamn", StartedAt,
                WordSubmissionStatus.Duplicate, "capital:copenhagen")
        };

        Assert.Equal(WordSubmissionStatus.Accepted, service.Validate(Capitals(), "Copenhagen", history).Status);
    }

    [Fact]
    public void ResultsAreDeterministicAndDoNotMutateChallengeState()
    {
        var category = Capitals();
        var challenge = CreateChallenge();
        var first = service.Validate(category, "Stockholm", challenge.Submissions);
        var again = service.Validate(category, "Stockholm", challenge.Submissions);

        Assert.Equal(first, again);
        Assert.Empty(challenge.Submissions);
        Assert.Equal(ChallengeOutcome.Pending, challenge.Outcome);
    }

    [Fact]
    public void ValidationUsesOnlyTheSelectedCategorySnapshot()
    {
        var cars = new CategoryDefinition(new Category("cars", "Cars", CategoryDifficulty.Easy),
            [new AcceptedAnswer("Volvo")]);

        Assert.Equal(WordSubmissionStatus.Rejected, service.Validate(Capitals(), "Volvo", []).Status);
        Assert.Equal(WordSubmissionStatus.Accepted, service.Validate(cars, "Volvo", []).Status);
        Assert.Equal(WordSubmissionStatus.Rejected, service.Validate(cars, "Copenhagen", []).Status);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void MatchingDoesNotDependOnTheServerCulture(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var category = new CategoryDefinition(new Category("cities", "Cities", CategoryDifficulty.Hard),
                [new AcceptedAnswer("ISTANBUL")]);
            Assert.Equal(WordSubmissionStatus.Accepted, service.Validate(category, "istanbul", []).Status);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TwoIndependentChallengesCanEachAcceptTheSameAnswer()
    {
        var category = Capitals();
        var firstChallenge = CreateChallenge();
        var first = service.Validate(category, "Copenhagen", firstChallenge.Submissions);
        firstChallenge.RecordSubmission(ToSubmission(firstChallenge, "Copenhagen", first));
        var other = CreateChallenge();
        Assert.Equal(WordSubmissionStatus.Accepted, service.Validate(category, "Köpenhamn", other.Submissions).Status);
    }

    [Fact]
    public void InvalidCallerArgumentsFailExplicitly()
    {
        Assert.Throws<ArgumentNullException>(() => service.Validate(null!, "word", []));
        Assert.Throws<ArgumentNullException>(() => service.Validate(Capitals(), "word", null!));
        Assert.Throws<ArgumentException>(() => service.Validate(Capitals(), "word", [null!]));
    }

    [Fact]
    public void RealValidationResultsDriveChallengeSuccessWithoutADatabase()
    {
        var category = Capitals();
        var challenge = CreateChallenge();
        foreach (var word in new[] { "Copenhagen", "Köpenhamn", "nonsense", "Stockholm" })
        {
            var result = service.Validate(category, word, challenge.Submissions);
            challenge.RecordSubmission(ToSubmission(challenge, word, result));
        }

        Assert.Equal(2, challenge.AcceptedWordCount);
        Assert.Collection(challenge.Submissions,
            word => Assert.Equal(WordSubmissionStatus.Accepted, word.Status),
            word => Assert.Equal(WordSubmissionStatus.Duplicate, word.Status),
            word => Assert.Equal(WordSubmissionStatus.Rejected, word.Status),
            word => Assert.Equal(WordSubmissionStatus.Accepted, word.Status));
        challenge.Resolve(ChallengeOutcome.Succeeded);
        Assert.Equal(ChallengeOutcome.Succeeded, challenge.Outcome);
    }

    private static CategoryDefinition Capitals() =>
        new(new Category("capitals", "Capitals", CategoryDifficulty.Easy),
        [
            new AcceptedAnswer("Copenhagen", ["Köpenhamn", "Kobenhavn"], "capital:copenhagen"),
            new AcceptedAnswer("Stockholm", id: "capital:stockholm")
        ]);

    private static Challenge CreateChallenge() =>
        new(Guid.NewGuid(), new Bid(Guid.NewGuid(), 2), Guid.NewGuid(), StartedAt, StartedAt.AddSeconds(30));

    private static SubmittedWord ToSubmission(Challenge challenge, string word, WordValidationResult result) =>
        new(challenge.Bid.PlayerId, word, StartedAt.AddSeconds(1), result.Status, result.CanonicalAnswerId);
}
