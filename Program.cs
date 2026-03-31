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

// Services
builder.Services.AddScoped<CountrySyncService>();
builder.Services.AddScoped<LeagueSyncService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<FixtureSyncService>();
builder.Services.AddScoped<BookmakerSyncService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<SyncStateService>();
builder.Services.AddScoped<PreloadSyncService>();
builder.Services.AddScoped<StandingsSyncService>();

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

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = "https://example.com/probs/internal-server-error",
            title = "An unexpected error occurred.",
            status = 500,
            detail = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? ex?.Message : null
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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballOddsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballOddsFixtureItem> Response { get; set; } = new();
}

public class ApiFootballOddsFixtureItem
{
    [JsonPropertyName("fixture")]
    public ApiFootballOddsFixture Fixture { get; set; } = new();

    [JsonPropertyName("bookmakers")]
    public List<ApiFootballOddsBookmaker> Bookmakers { get; set; } = new();
}

public class ApiFootballOddsFixture
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public class ApiFootballOddsBookmaker
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bets")]
    public List<ApiFootballOddsBet> Bets { get; set; } = new();
}

public class ApiFootballOddsBet
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<ApiFootballOddsValue> Values { get; set; } = new();
}

public class ApiFootballOddsValue
{
    // Use a flexible converter: API sometimes returns numbers instead of strings.
    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("odd")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Odd { get; set; } = string.Empty;
}

/// <summary>
/// Converter that accepts JSON string, number, boolean or null and returns a string.
/// Prevents System.Text.Json from throwing when API returns a number where we expect text.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => string.Empty,
            _ => reader.GetRawText()
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}