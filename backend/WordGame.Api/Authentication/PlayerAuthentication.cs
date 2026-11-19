using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using WordGame.Application.Sessions;

namespace WordGame.Api.Authentication;

internal static class PlayerAuthentication
{
    internal const string SessionClaim = "wordgame.session";

    internal static GuestSession? Session(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(SessionClaim), out var id)
            ? context.RequestServices.GetRequiredService<IGuestSessionStore>().Get(id) : null;

    internal static void AddPlayerAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "wordgame.player";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(1);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = context =>
            {
                var session = Guid.TryParse(context.Principal?.FindFirstValue(SessionClaim), out var sessionId)
                    ? context.HttpContext.RequestServices.GetRequiredService<IGuestSessionStore>().Get(sessionId) : null;
                if (session is null || context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) != session.PlayerId.ToString())
                {
                    context.RejectPrincipal();
                }
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization();
    }
}
