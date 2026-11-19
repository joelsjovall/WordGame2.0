using System.Globalization;
using System.Threading.RateLimiting;
using WordGame.Api.Errors;

namespace WordGame.Api.Authentication;

internal static class LobbyRateLimiting
{
    internal const string Create = "lobby-create";
    internal const string Join = "lobby-join";
    internal const string Guest = "guest-session";

    internal static void AddLobbyRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                await ApiProblems.WriteAsync(context.HttpContext,
                    ApiProblems.Create(context.HttpContext, 429, "Too many attempts. Please try again later."), cancellationToken);
            };
            AddPolicy(Create, "CreatePermitsPerMinute", 20);
            AddPolicy(Join, "JoinPermitsPerMinute", 30);
            AddPolicy(Guest, "GuestPermitsPerMinute", 60);

            void AddPolicy(string policy, string setting, int defaultPermits)
            {
                var permits = builder.Configuration.GetValue($"LobbyRateLimits:{setting}", defaultPermits);
                if (permits <= 0)
                {
                    throw new InvalidOperationException($"LobbyRateLimits:{setting} must be positive.");
                }
                options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                    // Never partition by a caller-supplied header, cookie, lobby code, or invitation.
                    context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            }
        });
    }
}
