using System.Globalization;

namespace WordGame.Domain.Tests;

public sealed class WordNormalizerTests
{
    [Theory]
    [InlineData(" Volvo ", "volvo")]
    [InlineData("BMW", "bmw")]
    [InlineData("  Audi", "audi")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" \t\r\n ", "")]
    [InlineData("\u00a0STOCKHOLM\u00a0", "stockholm")]
    [InlineData(" New  York ", "new york")]
    [InlineData("New\t\r\n York", "new york")]
    [InlineData("New\u00a0\u2003York", "new york")]
    [InlineData(" SÃO-PAULO ", "são-paulo")]
    public void NormalizationIgnoresCaseAndCollapsesWhitespace(string? input, string expected)
    {
        Assert.Equal(expected, WordNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void NormalizationDoesNotDependOnServerCulture(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("istanbul", WordNormalizer.Normalize(" ISTANBUL "));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void SubmittedWordUsesSharedNormalizationWithoutChangingCanonicalIdentity()
    {
        var submission = new SubmittedWord(Guid.NewGuid(), "  BMW  ", DomainTestData.StartedAt,
            WordSubmissionStatus.Accepted, "manufacturer:BMW");

        Assert.Equal("BMW", submission.Text);
        Assert.Equal("bmw", submission.NormalizedText);
        Assert.Equal("manufacturer:BMW", submission.CanonicalAnswerId);
    }
}
