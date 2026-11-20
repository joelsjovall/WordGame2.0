using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WordGame.Api.Tests.Fixtures;

// Each factory owns a fresh host and its in-memory sessions; no shared production services.
public sealed class WordGameApiFactory(string environment = "Production") : WebApplicationFactory<Program>
{
    private readonly string statisticsDirectory = Path.Combine(Path.GetTempPath(), "wordgame-tests", Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Statistics:Directory"] = statisticsDirectory
        }));
        builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
    }
}
