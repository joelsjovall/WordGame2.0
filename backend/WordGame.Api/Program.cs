using System.Text.Json;
using System.Text.Json.Serialization;
using WordGame.Api.Authentication;
using WordGame.Api.Endpoints;
using WordGame.Api.Errors;
using WordGame.Api.OpenApi;
using WordGame.Api.Sessions;
using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Synchronization;
using WordGame.Application.Words;
using WordGame.Domain;
using WordGame.Infrastructure.Categories;
using WordGame.Infrastructure.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<ContractDocumentTransformer>());
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.AddPlayerAuthentication();
builder.Services.AddSingleton<GameEventService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => new SessionRetention(
    builder.Configuration.GetValue("Sessions:DisconnectedGrace", TimeSpan.FromMinutes(2)),
    builder.Configuration.GetValue("Sessions:CompletedRetention", TimeSpan.FromMinutes(10)),
    builder.Configuration.GetValue("Sessions:AbandonedRetention", TimeSpan.FromMinutes(5))));
builder.Services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
builder.Services.AddSingleton(_ => new CategorySelectionRules(
    builder.Configuration.GetValue("Gameplay:CategorySelectionTimeoutEnabled", true)
        ? builder.Configuration.GetValue("Gameplay:CategorySelectionTimeout", TimeSpan.FromSeconds(45)) : null,
    builder.Configuration.GetSection("Gameplay:AllowedDifficulties").Get<CategoryDifficulty[]>(),
    Enum.GetValues<CategoryDifficulty>().ToDictionary(difficulty => difficulty,
        difficulty => builder.Configuration.GetValue($"Gameplay:DifficultyMultipliers:{difficulty}", (int)difficulty + 1)),
    builder.Configuration.GetValue("Gameplay:CategorySelectionTimeoutBehavior", CategoryTimeoutBehavior.AutoSelect)));
builder.Services.AddSingleton<CategorySelectionService>();
builder.Services.AddSingleton(_ => new ChallengeRules(builder.Configuration.GetValue("Gameplay:ChallengeDuration", TimeSpan.FromSeconds(30))));
builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton(_ => new ScoringRules(builder.Configuration.GetValue("Gameplay:Scoring:OutcomeBonus", 50)));
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddHostedService<SessionCleanupWorker>();
builder.Services.AddHostedService<GameplayTimeoutWorker>();
builder.Services.AddSingleton<IWordValidationService, WordValidationService>();
builder.Services.AddSingleton<ICategoryProvider>(_ => new JsonCategoryProvider(
    Path.GetFullPath(builder.Configuration["Categories:Directory"] ?? "Data/Categories",
        builder.Environment.ContentRootPath)));

if (builder.Environment.IsDevelopment())
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

var app = builder.Build();

// Validate the complete dataset before accepting traffic; later lookups perform no file I/O.
_ = app.Services.GetRequiredService<ICategoryProvider>();

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
    {
        await ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext,
            context.HttpContext.Response.StatusCode,
            Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(context.HttpContext.Response.StatusCode)),
            context.HttpContext.RequestAborted);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapCategoryEndpoints();
app.MapGameSynchronizationEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point for WebApplicationFactory integration tests.
public partial class Program;
