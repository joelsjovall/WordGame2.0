using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WordGame.Api.Tests.Fixtures;

internal static class AuthenticatedPlayerClient
{
    internal static HttpClient Create(WebApplicationFactory<Program> factory, Guid playerId, DateTimeOffset? expires = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, playerId.ToString())], scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresUtc = expires ?? DateTimeOffset.UtcNow.AddHours(1)
        }, scheme);
        // Test-only server-issued ticket; no login route or credential is committed.
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        return client;
    }
}
