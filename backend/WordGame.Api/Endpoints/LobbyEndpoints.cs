using System.Security.Claims;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Api.Mapping;
using WordGame.Application.Lobbies;

namespace WordGame.Api.Endpoints;

internal static class LobbyEndpoints
{
    internal static void MapLobbyEndpoints(this WebApplication app) =>
        app.MapPost("/api/v1/lobbies", CreateAsync).RequireAuthorization().AddEndpointFilter<SameOriginWriteFilter>()
            .WithTags("Lobbies").WithName("CreatePrivateLobby")
            .Produces<LobbyResponse>(201).Produces<LobbyResponse>(200).Produces(400).Produces(401).Produces(403).Produces(409);

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
