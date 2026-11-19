using System.Globalization;
using System.Security.Claims;
using WordGame.Api.Errors;
using WordGame.Api.Mapping;
using WordGame.Domain;

namespace WordGame.Api.Endpoints;

internal static class LobbyRequests
{
    internal static Guid Actor(HttpContext context) => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ? actor : Guid.Empty;
    internal static long Version(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version) &&
        version >= 0 && value == version.ToString(CultureInfo.InvariantCulture)
        ? version : throw new ArgumentException("Provide a canonical nonnegative lobby version.");

    internal static async Task<IResult> ExecuteAsync(HttpContext context, Func<Task<Lobby?>> action)
    {
        if (Actor(context) == Guid.Empty)
        {
            return TypedResults.Unauthorized();
        }

        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var result = await action();
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(LobbyMapper.Map(result, context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow()));
        }
        catch (LobbyActionException exception)
        {
            return Problem(context, exception.Forbidden ? 403 : 409, exception.Message);
        }
        catch (ArgumentException exception) { return Problem(context, 400, exception.Message); }
    }
    private static IResult Problem(HttpContext context, int status, string title) =>
        TypedResults.Json(ApiProblems.Create(context, status, title), statusCode: status, contentType: "application/problem+json");
}
