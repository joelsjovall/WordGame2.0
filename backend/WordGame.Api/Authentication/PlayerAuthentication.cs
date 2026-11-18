using Microsoft.AspNetCore.Authentication.Cookies;

namespace WordGame.Api.Authentication;

internal static class PlayerAuthentication
{
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
