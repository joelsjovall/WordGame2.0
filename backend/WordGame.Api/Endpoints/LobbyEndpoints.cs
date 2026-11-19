using System.Security.Claims;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Api.Mapping;
using WordGame.Application.Lobbies;
using WordGame.Domain;

namespace WordGame.Api.Endpoints;

internal static class LobbyEndpoints
{
    internal static void MapLobbyEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/lobbies", CreateAsync).RequireAuthorization().AddEndpointFilter<SameOriginWriteFilter>()
            .WithTags("Lobbies").WithName("CreatePrivateLobby")
            .Produces<LobbyResponse>(201).Produces<LobbyResponse>(200).Produces(400).Produces(401).Produces(403).Produces(409);
        app.MapPost("/api/v1/lobbies/join", JoinAsync).RequireAuthorization().AddEndpointFilter<SameOriginWriteFilter>()
            .WithTags("Lobbies").WithName("JoinPrivateLobby").Produces<LobbyResponse>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static async Task<IResult> JoinAsync(JoinLobbyRequest request, HttpContext context, LobbyService lobbies,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId) || playerId == Guid.Empty)
        {
            return TypedResults.Unauthorized();
        }

        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var lobby = await lobbies.JoinAsync(playerId, request.LobbyCode, request.DisplayName, request.LobbyId, cancellationToken);
            return lobby is null ? Problem(context, 404, "This lobby code or invitation is invalid or expired.") : TypedResults.Ok(LobbyMapper.Map(lobby));
        }
        catch (LobbyJoinException exception)
        {
            return Problem(context, 409, exception.Reason switch
            {
                LobbyJoinFailure.Full => "This lobby is full.",
                LobbyJoinFailure.DisplayNameTaken => "That display name is already in use in this lobby.",
                _ => "This player has already joined with a different display name."
            });
        }
        catch (ArgumentException exception)
        {
            return Problem(context, 400, exception.ParamName switch
            {
                "value" => "Enter a six-character lobby code using unambiguous letters and digits.",
                "expectedLobbyId" => "This invitation has an invalid lobby ID.",
                _ => "Provide a nonblank display name of at most 32 characters without control characters."
            });
        }
    }

    private static IResult Problem(HttpContext context, int status, string title) =>
        TypedResults.Json(ApiProblems.Create(context, status, title), statusCode: status, contentType: "application/problem+json");

    private static async Task<IResult> CreateAsync(CreateLobbyRequest request, HttpContext context, LobbyService lobbies,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId) || playerId == Guid.Empty)
        {
            return TypedResults.Unauthorized();
        }

        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var creation = await lobbies.CreateAsync(playerId, request.RequestId, request.DisplayName, cancellationToken);
            return TypedResults.Json(LobbyMapper.Map(creation.Lobby), statusCode: creation.WasCreated ? 201 : 200);
        }
        catch (LobbyCreationConflictException)
        {
            return TypedResults.Json(ApiProblems.Create(context, 409, "This request ID was already used with a different display name."),
                statusCode: 409, contentType: "application/problem+json");
        }
        catch (ArgumentException exception)
        {
            var field = exception.ParamName == "requestId" ? "requestId" : "displayName";
            var message = field == "requestId" ? "Provide a nonempty request ID."
                : "Provide a nonblank display name of at most 32 characters without control characters.";
            return TypedResults.Json(ApiProblems.Create(context, 400, "Invalid lobby request.",
                new Dictionary<string, string[]> { [field] = [message] }), statusCode: 400, contentType: "application/problem+json");
        }
    }
}
