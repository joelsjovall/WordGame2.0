using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WordGame.Application.Categories;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class ApiFoundationTests
{
    [Fact]
    public async Task HostedCleanupAutomaticallyRemovesExpiredSessions()
    {
        using var factory = CreateFactory("Production").WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sessions:DisconnectedGrace"] = "00:00:00",
                    ["Sessions:AbandonedRetention"] = "00:00:00",
                    ["Sessions:CleanupInterval"] = "00:00:00.020"
                })));
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var session = await store.CreateAsync(
            [new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob")]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await store.GetAsync(session.Id, timeout.Token) is not null)
        {
            await Task.Delay(10, timeout.Token);
        }
        Assert.Null(await store.GetAsync(session.Id));
    }

    [Fact]
    public async Task ShippedDatasetsAreRegisteredAndUsableByTheValidator()
    {
        using var factory = CreateFactory("Production");
        var provider = factory.Services.GetRequiredService<ICategoryProvider>();
        var catalog = await provider.GetCategoriesAsync();
        Assert.Equal(3, catalog.Count);
        Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Easy));
        Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Medium));
        Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Hard));
        var category = await provider.GetByIdAsync("european-capitals");
        Assert.NotNull(category);
        var result = factory.Services.GetRequiredService<IWordValidationService>()
            .Validate(category, "Köpenhamn", []);
        Assert.Equal(WordSubmissionStatus.Accepted, result.Status);
        Assert.Same(category, await provider.GetByIdAsync("european-capitals"));
    }

    [Fact]
    public void MissingConfiguredDatasetDirectoryPreventsStartup()
    {
        var missing = Path.Combine(Path.GetTempPath(), "wordgame-f007-missing-" + Guid.NewGuid().ToString("N"));
        using var factory = CreateFactory("Production").WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Categories:Directory"] = missing })));

        Assert.Throws<DirectoryNotFoundException>(() => factory.CreateClient());
    }

    [Fact]
    public void ValidationServiceIsRegisteredWithoutDatabaseOrProviderDependencies()
    {
        using var factory = CreateFactory("Production");
        var service = factory.Services.GetRequiredService<IWordValidationService>();
        var category = new CategoryDefinition(
            new Category("cars", "Cars", CategoryDifficulty.Easy), [new AcceptedAnswer("Volvo")]);

        Assert.Equal(WordSubmissionStatus.Accepted, service.Validate(category, " VOLVO ", []).Status);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Health_ReturnsOkWithoutDatabase(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    public async Task DevelopmentCors_AllowsConfiguredFrontendPreflight(string origin)
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.Contains("content-type", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("Development", "https://untrusted.example")]
    [InlineData("Production", "http://localhost:5173")]
    public async Task Cors_DoesNotAllowUntrustedOrProductionOrigins(string environment, string origin)
    {
        using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("Development", "application/json")]
    [InlineData("Production", "application/json")]
    [InlineData("Production", "text/html")]
    public async Task UnhandledException_ReturnsProblemDetailsWithoutExceptionDetails(string environment, string accept)
    {
        using var factory = CreateFactory(environment).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<HealthCheckService, ThrowingHealthCheckService>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("test-private-exception-detail", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment));

    private sealed class ThrowingHealthCheckService : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("test-private-exception-detail");
    }
}
