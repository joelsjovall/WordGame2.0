using System.Threading.Channels;
using WordGame.Application.Sessions;

namespace WordGame.Infrastructure.Sessions;

internal sealed class SessionEntry(GameSession session)
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal GameSession Session { get; set; } = session;
    internal long Version { get; set; }
    internal bool Removed { get; set; }
    internal Dictionary<Guid, Channel<SessionChange>> Subscribers { get; } = [];

    // All callers hold Gate. Notifications never execute subscriber callbacks inside an update.
    internal void Publish(SessionChangeKind kind)
    {
        var change = new SessionChange(Session.Id, Version, Session.Status, kind);
        foreach (var channel in Subscribers.Values)
        {
            channel.Writer.TryWrite(change);
            if (kind == SessionChangeKind.Removed)
            {
                channel.Writer.TryComplete();
            }
        }
    }
}
