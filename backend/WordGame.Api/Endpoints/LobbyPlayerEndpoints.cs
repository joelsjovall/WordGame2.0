using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Application.Lobbies;
using static WordGame.Api.Endpoints.LobbyRequests;

namespace WordGame.Api.Endpoints;

internal static class LobbyPlayerEndpoints
{
    internal static void MapLobbyPlayerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/lobbies/{lobbyId:guid}").RequireAuthorization().WithTags("Lobbies")
            .AddEndpointFilter<SameOriginWriteFilter>();
        group.MapPost("/ready", ReadyAsync).WithName("SetLobbyReady").Produces<LobbyResponse>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
        group.MapPost("/leave", LeaveAsync).WithName("LeaveLobby").Produces<LobbyResponse>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
        group.MapPost("/remove", RemoveAsync).WithName("RemoveLobbyPlayer").Produces<LobbyResponse>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static Task<IResult> ReadyAsync(Guid lobbyId, SetLobbyReadyRequest request, HttpContext context,
        LobbyPlayerService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.SetReadyAsync(lobbyId, Actor(context), Version(request.ExpectedVersion), request.IsReady, cancellationToken));
    private static Task<IResult> LeaveAsync(Guid lobbyId, LeaveLobbyRequest _, HttpContext context,
        LobbyPlayerService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.LeaveAsync(lobbyId, Actor(context), cancellationToken));
    private static Task<IResult> RemoveAsync(Guid lobbyId, RemoveLobbyPlayerRequest request, HttpContext context,
        LobbyPlayerService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.RemoveAsync(lobbyId, Actor(context), request.PlayerId, cancellationToken));
}
