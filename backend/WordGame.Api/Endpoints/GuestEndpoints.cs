using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Application.Sessions;

namespace WordGame.Api.Endpoints;

internal static class GuestEndpoints
{
    internal static void MapGuestEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/guests", CreateAsync).AllowAnonymous().AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Guest).Produces(429)
            .WithTags("Guests").WithName("EnsureGuestIdentity").Produces<GuestIdentityResponse>().Produces(400).Produces(403);
        app.MapPost("/api/v1/guests/reset", ResetAsync).RequireAuthorization().AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Guest).WithTags("Guests").WithName("ResetGuestSession")
            .Produces(204).Produces(400).Produces(401).Produces(403).Produces(429);
    }

    private static async Task<IResult> CreateAsync(CreateGuestRequest _, HttpContext context, IGuestSessionStore sessions)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId) || playerId == Guid.Empty)
        {
            playerId = Guid.NewGuid();
            var session = sessions.Issue(playerId);
            var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, playerId.ToString()),
                new Claim(PlayerAuthentication.SessionClaim, session.Id.ToString())], scheme));
            await context.SignInAsync(scheme, principal, new AuthenticationProperties
            { IsPersistent = true, IssuedUtc = session.IssuedAt, ExpiresUtc = session.ExpiresAt });
        }
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new GuestIdentityResponse(playerId));
    }

    private static async Task<IResult> ResetAsync(CreateGuestRequest _, HttpContext context, IGuestSessionStore sessions)
    {
        var session = PlayerAuthentication.Session(context);
        if (session is null)
        {
            return TypedResults.Unauthorized();
        }
        sessions.Revoke(session.Id);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.NoContent();
    }
}
