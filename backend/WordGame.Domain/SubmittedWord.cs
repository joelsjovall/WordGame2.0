namespace WordGame.Domain;

public sealed record SubmittedWord
{
    public SubmittedWord(
        Guid playerId,
        string text,
        DateTimeOffset submittedAt,
        WordSubmissionStatus status,
        string? canonicalAnswerId = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        PlayerId = DomainGuard.Id(playerId, nameof(playerId));
        Text = DomainGuard.Text(text, nameof(text));
        SubmittedAt = submittedAt;
        Status = status;
        CanonicalAnswerId = status == WordSubmissionStatus.Accepted
            ? DomainGuard.Text(canonicalAnswerId!, nameof(canonicalAnswerId))
            : canonicalAnswerId;
    }

    public Guid PlayerId { get; }
    public string Text { get; }
    public DateTimeOffset SubmittedAt { get; }
    public WordSubmissionStatus Status { get; }
    public string? CanonicalAnswerId { get; }
}
