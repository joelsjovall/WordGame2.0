using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WordGame.Api.Tests.Fixtures;

// Each factory owns a fresh host and its in-memory sessions; no shared production services.
public sealed class WordGameApiFactory(string environment = "Production") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
    }
}
