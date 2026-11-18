namespace WordGame.Domain.Tests;

public sealed class CategoryDefinitionTests
{
    [Fact]
    public void CanonicalValuesAndAliasesResolveToOneAnswerIdentity()
    {
        var answer = new AcceptedAnswer("Copenhagen", ["Köpenhamn", "Kobenhavn"], "capital:copenhagen");
        var definition = Define(answer);

        Assert.Same(answer, definition.FindAnswer(" COPENHAGEN "));
        Assert.Same(answer, definition.FindAnswer(" köpenhamn "));
        Assert.Same(answer, definition.FindAnswer("KOBENHAVN"));
        Assert.Equal("capital:copenhagen", answer.Id);
        Assert.Null(definition.FindAnswer("not-a-capital"));
        Assert.Null(definition.FindAnswer(null));
    }

    [Fact]
    public void AnswerIdentityDefaultsToNormalizedCanonicalValue()
    {
        var answer = new AcceptedAnswer(" New  York ");
        Assert.Equal("new york", answer.Id);
        Assert.Equal("New  York", answer.Value);
        Assert.Empty(answer.Aliases);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData(null)]
    public void MissingCanonicalValuesAndAliasesAreInvalidData(string? text)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AcceptedAnswer(text!));
        Assert.ThrowsAny<ArgumentException>(() => new AcceptedAnswer("Stockholm", [text!]));
    }

    [Fact]
    public void ExplicitAnswerIdentityCannotBeBlank()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AcceptedAnswer("Stockholm", id: " "));
    }

    [Fact]
    public void CategoriesRequireMetadataAndAtLeastOneAnswer()
    {
        var metadata = new Category("capitals", "Capitals", CategoryDifficulty.Easy);
        Assert.Throws<ArgumentNullException>(() => new CategoryDefinition(null!, [new AcceptedAnswer("Stockholm")]));
        Assert.Throws<ArgumentNullException>(() => new CategoryDefinition(metadata, null!));
        Assert.Throws<ArgumentException>(() => new CategoryDefinition(metadata, []));
        Assert.Throws<ArgumentException>(() => new CategoryDefinition(metadata, [null!]));
    }

    [Fact]
    public void AnswerIdsMustBeUniqueWithinACategory()
    {
        Assert.Throws<ArgumentException>(() => Define(
            new AcceptedAnswer("Stockholm", id: "same"),
            new AcceptedAnswer("Paris", id: "same")));
    }

    [Theory]
    [InlineData("Stockholm", " STOCKHOLM ")]
    [InlineData("New York", "New\t  York")]
    [InlineData("ΟΣ", "ος")]
    public void CollidingCanonicalValuesAreRejectedRatherThanSelectingTheFirst(string first, string second)
    {
        var exception = Assert.Throws<ArgumentException>(() => Define(
            new AcceptedAnswer(first, id: "first"),
            new AcceptedAnswer(second, id: "second")));

        Assert.Contains("ambiguous", exception.Message);
        Assert.Contains("capitals", exception.Message);
    }

    [Fact]
    public void AliasesCannotHideAnotherAnswersCanonicalValue()
    {
        Assert.Throws<ArgumentException>(() => Define(
            new AcceptedAnswer("Copenhagen", ["Stockholm"]),
            new AcceptedAnswer("Stockholm")));
    }

    [Fact]
    public void AliasesCannotResolveToDifferentAnswers()
    {
        Assert.Throws<ArgumentException>(() => Define(
            new AcceptedAnswer("First", [" shared  name "]),
            new AcceptedAnswer("Second", ["SHARED\tNAME"])));
    }

    [Fact]
    public void RedundantAliasesForTheSameAnswerAreHarmless()
    {
        var answer = new AcceptedAnswer("Copenhagen", ["Copenhagen", "copenhagen", "Köpenhamn", "Köpenhamn"]);
        var definition = Define(answer);
        Assert.Same(answer, definition.FindAnswer("KÖPENHAMN"));
        Assert.Same(answer, definition.FindAnswer("Copenhagen"));
        Assert.Single(definition.Answers);
    }

    [Fact]
    public void SnapshotAndLookupCannotBeChangedThroughInputCollections()
    {
        var aliases = new[] { "Köpenhamn" };
        var answer = new AcceptedAnswer("Copenhagen", aliases);
        var answers = new[] { answer };
        var definition = Define(answers);
        aliases[0] = "Wrong";
        answers[0] = new AcceptedAnswer("Wrong");

        Assert.Same(answer, Assert.Single(definition.Answers));
        Assert.Same(answer, definition.FindAnswer("Köpenhamn"));
        Assert.Null(definition.FindAnswer("Wrong"));
        Assert.Throws<NotSupportedException>(() => ((IList<AcceptedAnswer>)definition.Answers).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)answer.Aliases).Clear());
    }

    private static CategoryDefinition Define(params AcceptedAnswer[] answers) =>
        new(new Category("capitals", "Capitals", CategoryDifficulty.Easy), answers);
}
