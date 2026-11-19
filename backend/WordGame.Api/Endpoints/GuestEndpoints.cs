using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;

namespace WordGame.Api.Endpoints;

internal static class GuestEndpoints
{
    internal static void MapGuestEndpoints(this WebApplication app) =>
        app.MapPost("/api/v1/guests", CreateAsync).AllowAnonymous().AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Guest).Produces(429)
            .WithTags("Guests").WithName("EnsureGuestIdentity").Produces<GuestIdentityResponse>().Produces(400).Produces(403);

    private static async Task<IResult> CreateAsync(CreateGuestRequest _, HttpContext context)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId) || playerId == Guid.Empty)
        {
            playerId = Guid.NewGuid();
            var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, playerId.ToString())], scheme));
            await context.SignInAsync(scheme, principal, new AuthenticationProperties { IsPersistent = true });
        }
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new GuestIdentityResponse(playerId));
    }
}
