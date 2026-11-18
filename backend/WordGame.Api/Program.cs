using WordGame.Api.Errors;
using WordGame.Application.Categories;
using WordGame.Application.Words;
using WordGame.Infrastructure.Categories;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IWordValidationService, WordValidationService>();
builder.Services.AddSingleton<ICategoryProvider>(_ => new JsonCategoryProvider(
    Path.GetFullPath(builder.Configuration["Categories:Directory"] ?? "Data/Categories",
        builder.Environment.ContentRootPath)));

if (builder.Environment.IsDevelopment())
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

// Validate the complete dataset before accepting traffic; later lookups perform no file I/O.
_ = app.Services.GetRequiredService<ICategoryProvider>();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point for WebApplicationFactory integration tests.
public partial class Program;
