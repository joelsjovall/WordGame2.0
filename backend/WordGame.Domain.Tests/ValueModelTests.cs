namespace WordGame.Domain.Tests;

public sealed class ValueModelTests
{
    [Fact]
    public void TextModelsTrimDisplayValuesAndPreserveIdentity()
    {
        var id = Guid.NewGuid();
        var player = new Player(id, "  Alice  ");
        var category = new Category("  capitals  ", "  Capitals  ", CategoryDifficulty.Hard);

        Assert.Equal(id, player.Id);
        Assert.Equal("Alice", player.DisplayName);
        Assert.Equal("capitals", category.Id);
        Assert.Equal("Capitals", category.Name);
        Assert.Equal(CategoryDifficulty.Hard, category.Difficulty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void TextModelsRejectMissingNames(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Player(Guid.NewGuid(), name!));
        Assert.ThrowsAny<ArgumentException>(() => new Category("id", name!, CategoryDifficulty.Easy));
        Assert.ThrowsAny<ArgumentException>(() => new Category(name!, "name", CategoryDifficulty.Easy));
    }

    [Fact]
    public void ModelsRejectEmptyPlayerIdentity()
    {
        Assert.Throws<ArgumentException>(() => new Player(Guid.Empty, "Alice"));
        Assert.Throws<ArgumentException>(() => new Bid(Guid.Empty, 1));
        Assert.Throws<ArgumentException>(() => new PlayerTurn(Guid.Empty, 1));
        Assert.Throws<ArgumentException>(() => new PlayerScore(Guid.Empty, 0));
        Assert.Throws<ArgumentException>(() =>
            new SubmittedWord(Guid.Empty, "word", DomainTestData.StartedAt, WordSubmissionStatus.Rejected));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BidsAndTurnNumbersMustBePositive(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bid(Guid.NewGuid(), value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerTurn(Guid.NewGuid(), value));
    }

    [Fact]
    public void ScoresAllowZeroButRejectNegativePoints()
    {
        Assert.Equal(0, new PlayerScore(Guid.NewGuid(), 0).Points);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerScore(Guid.NewGuid(), -1));
    }

    [Fact]
    public void AcceptedSubmissionRequiresCanonicalAnswerIdentity()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SubmittedWord(
            Guid.NewGuid(), "Stockholm", DomainTestData.StartedAt, WordSubmissionStatus.Accepted));

        var submission = new SubmittedWord(Guid.NewGuid(), "  Stockholm  ", DomainTestData.StartedAt,
            WordSubmissionStatus.Accepted, "capital:stockholm");
        Assert.Equal("Stockholm", submission.Text);
        Assert.Equal("capital:stockholm", submission.CanonicalAnswerId);
    }

    [Fact]
    public void ModelsRejectUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Category("id", "name", (CategoryDifficulty)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SubmittedWord(Guid.NewGuid(), "word", DomainTestData.StartedAt, (WordSubmissionStatus)99));
    }
}
