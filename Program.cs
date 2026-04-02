using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SmartBets.Data;
using SmartBets.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Swagger + API Key auth
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartBets API",
        Version = "v1"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Enter your API key",
        Type = SecuritySchemeType.ApiKey,
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// HTTP client
builder.Services.AddHttpClient<FootballApiService>();
builder.Services.Configure<LiveAutomationOptions>(builder.Configuration.GetSection("LiveAutomation"));
builder.Services.Configure<CoreDataAutomationOptions>(builder.Configuration.GetSection("CoreDataAutomation"));
builder.Services.Configure<ApiFootballClientOptions>(builder.Configuration.GetSection("ApiFootballClient"));
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
builder.Services.AddSingleton<ApiFootballQuotaTelemetryService>();
builder.Services.AddSingleton<CoreLeagueCatalogState>();
builder.Services.AddSingleton<CoreAutomationQuotaManager>();

// Services
builder.Services.AddScoped<CountrySyncService>();
builder.Services.AddScoped<LeagueSyncService>();
builder.Services.AddScoped<LeagueCoverageService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<FixtureSyncService>();
builder.Services.AddScoped<FixtureLiveStatusSyncService>();
builder.Services.AddScoped<FixtureMatchCenterReadService>();
builder.Services.AddScoped<FixtureMatchCenterSyncService>();
builder.Services.AddScoped<FixturePreviewReadService>();
builder.Services.AddScoped<FixturePreviewSyncService>();
builder.Services.AddScoped<TeamAnalyticsService>();
builder.Services.AddScoped<LeagueAnalyticsService>();
builder.Services.AddScoped<BookmakerSyncService>();
builder.Services.AddScoped<PreMatchOddsService>();
builder.Services.AddScoped<LiveOddsService>();
builder.Services.AddScoped<OddsAnalyticsService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<SyncErrorService>();
builder.Services.AddScoped<SyncStateService>();
builder.Services.AddScoped<PreloadSyncService>();
builder.Services.AddScoped<HistoricalBootstrapService>();
builder.Services.AddScoped<StandingsSyncService>();
builder.Services.AddScoped<LiveAutomationOrchestrator>();
builder.Services.AddScoped<CoreDataAutomationOrchestrator>();
builder.Services.AddScoped<CoreAutomationCatalogRefreshJobService>();
builder.Services.AddScoped<CoreAutomationTeamsRollingJobService>();
builder.Services.AddScoped<CoreAutomationFixturesRollingJobService>();
builder.Services.AddScoped<CoreAutomationOddsPreMatchJobService>();
builder.Services.AddScoped<CoreAutomationOddsLiveJobService>();
builder.Services.AddScoped<CoreAutomationRepairJobService>();
builder.Services.AddScoped<DataRetentionService>();
builder.Services.AddHostedService<CoreDataAutomationBackgroundService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();

// CORS
var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// 🔥 Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = exceptionHandlerPathFeature?.Error;

        logger.LogError(ex, "Unhandled exception occurred while processing request {Path}", context.Request.Path);

        if (ex is not null)
        {
            try
            {
                var syncErrorService = context.RequestServices.GetRequiredService<SyncErrorService>();
                await syncErrorService.TryRecordRequestFailureAsync(context, ex, context.RequestAborted);
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "Failed to persist sync error for request {Path}", context.Request.Path);
            }
        }

        var (statusCode, title, detail) = ex switch
        {
            InvalidOperationException invalidOp when IsMissingApiFootballConfig(invalidOp) => (
                StatusCodes.Status503ServiceUnavailable,
                "API-Football configuration is missing.",
                invalidOp.Message),
            InvalidOperationException invalidOp when IsRateLimitError(invalidOp) => (
                StatusCodes.Status429TooManyRequests,
                "API-Football rate limit reached.",
                invalidOp.Message),
            InvalidOperationException invalidOp when IsNotFoundLike(invalidOp) => (
                StatusCodes.Status404NotFound,
                "Requested resource was not found.",
                invalidOp.Message),
            InvalidOperationException invalidOp => (
                StatusCodes.Status400BadRequest,
                "The request could not be completed.",
                invalidOp.Message),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                "The request is invalid.",
                argumentException.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? ex?.Message : null)
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = statusCode == 500
                ? "https://example.com/probs/internal-server-error"
                : "https://example.com/probs/request-error",
            title,
            status = statusCode,
            detail
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Render port fix
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://*:{port}");

// Swagger (public)
app.UseSwagger();
app.UseSwaggerUI();

// CORS
app.UseCors("FrontendPolicy");

// 🔐 API Key Middleware
app.Use(async (context, next) =>
{
    var expectedToken = builder.Configuration["ApiAuth:Token"];

    // ако няма token → allow (dev)
    if (string.IsNullOrWhiteSpace(expectedToken))
    {
        await next();
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;

    // публични endpoints
    if (path.StartsWith("/swagger") || path.StartsWith("/ping"))
    {
        await next();
        return;
    }

    var requestToken = context.Request.Headers["X-API-KEY"].FirstOrDefault();

    if (requestToken != expectedToken)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized - Invalid or missing API Key");
        return;
    }

    await next();
});

app.UseAuthorization();

// Endpoints
app.MapControllers();

// ✅ публичен health check
app.MapGet("/ping", () => "pong");

app.Run();

static bool IsMissingApiFootballConfig(InvalidOperationException exception)
{
    return exception.Message.Contains("ApiFootball:BaseUrl", StringComparison.OrdinalIgnoreCase) ||
           exception.Message.Contains("ApiFootball:ApiKey", StringComparison.OrdinalIgnoreCase);
}

static bool IsRateLimitError(InvalidOperationException exception)
{
    return exception.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
}

static bool IsNotFoundLike(InvalidOperationException exception)
{
    return exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
