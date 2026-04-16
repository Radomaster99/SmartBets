using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartBets.Data;
using SmartBets.Hubs;
using SmartBets.Services;

var builder = WebApplication.CreateBuilder(args);
var apiKeyToken = builder.Configuration["ApiAuth:Token"];
var jwtSigningKey = builder.Configuration["JwtAuth:SigningKey"];
var adminAuthOptions = builder.Configuration.GetSection("AdminAuth").Get<AdminAuthOptions>() ?? new AdminAuthOptions();
var effectiveJwtSigningKeyBytes = JwtSigningKeyHelper.ResolveSigningKeyBytes(jwtSigningKey, apiKeyToken);
var authEnabled = !string.IsNullOrWhiteSpace(apiKeyToken) || !string.IsNullOrWhiteSpace(jwtSigningKey);
var isDevelopment = builder.Environment.IsDevelopment();
var swaggerEnabled = isDevelopment || builder.Configuration.GetValue<bool>("Swagger:Enabled");

// Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSignalR();

// Swagger + auth
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

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
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
builder.Services.AddHttpClient<TheOddsApiService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<CoreDataAutomationOptions>(builder.Configuration.GetSection("CoreDataAutomation"));
builder.Services.Configure<ApiFootballClientOptions>(builder.Configuration.GetSection("ApiFootballClient"));
builder.Services.Configure<TheOddsApiOptions>(builder.Configuration.GetSection("TheOddsApi"));
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("JwtAuth"));
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection("AdminAuth"));
builder.Services.AddSingleton<ApiFootballQuotaTelemetryService>();
builder.Services.AddSingleton<CoreLeagueCatalogState>();
builder.Services.AddSingleton<CoreAutomationQuotaManager>();
builder.Services.AddSingleton<PreMatchOddsAttemptTrackerService>();
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "SmartAuth";
        options.DefaultChallengeScheme = "SmartAuth";
    })
    .AddPolicyScheme("SmartAuth", "JWT or API key", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            if (context.Request.Path.StartsWithSegments(LiveOddsHub.Route))
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(accessToken) && LooksLikeJwt(accessToken))
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
            }

            if (!string.IsNullOrWhiteSpace(context.Request.Headers["X-API-KEY"]))
                return ApiKeyAuthenticationHandler.SchemeName;

            if (context.Request.Cookies.ContainsKey(adminAuthOptions.GetCookieName()))
                return AdminAuthService.AdminCookieScheme;

            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(effectiveJwtSigningKeyBytes),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtAuth:Issuer"] ?? "SmartBets",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtAuth:Audience"] ?? "SmartBets.Frontend",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(LiveOddsHub.Route))
                {
                    var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(accessToken) && LooksLikeJwt(accessToken))
                    {
                        context.Token = accessToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddCookie(AdminAuthService.AdminCookieScheme, options =>
    {
        options.Cookie.Name = adminAuthOptions.GetCookieName();
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = adminAuthOptions.GetCookieSameSite();
        options.Cookie.SecurePolicy = isDevelopment
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = adminAuthOptions.GetSessionLifetime();
        options.SlidingExpiration = true;

        if (!string.IsNullOrWhiteSpace(adminAuthOptions.CookieDomain))
        {
            options.Cookie.Domain = adminAuthOptions.CookieDomain.Trim();
        }

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    if (!authEnabled)
        return;

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("SmartAuth")
        .RequireAuthenticatedUser()
        .Build();
});

// Services
builder.Services.AddScoped<CountrySyncService>();
builder.Services.AddScoped<LeagueSyncService>();
builder.Services.AddScoped<LeagueCoverageService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<FixtureSyncService>();
builder.Services.AddScoped<FixtureLiveStatusSyncService>();
builder.Services.AddScoped<FixtureMatchCenterReadService>();
builder.Services.AddScoped<FixtureMatchCenterSyncService>();
builder.Services.AddScoped<FixtureLiveStatisticsAutoRefreshService>();
builder.Services.AddScoped<FixturePreviewReadService>();
builder.Services.AddScoped<FixturePreviewSyncService>();
builder.Services.AddScoped<TeamAnalyticsService>();
builder.Services.AddScoped<LeagueAnalyticsService>();
builder.Services.AddScoped<BookmakerSyncService>();
builder.Services.AddScoped<PreMatchOddsService>();
builder.Services.AddScoped<TheOddsSportKeyResolverService>();
builder.Services.AddScoped<TheOddsLiveOddsService>();
builder.Services.AddSingleton<TheOddsViewerActivityService>();
builder.Services.AddSingleton<TheOddsViewerRefreshStateService>();
builder.Services.AddScoped<LiveOddsService>();
builder.Services.AddScoped<OddsAnalyticsService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<SyncErrorService>();
builder.Services.AddScoped<SyncStateService>();
builder.Services.AddScoped<PreloadSyncService>();
builder.Services.AddScoped<HistoricalBootstrapService>();
builder.Services.AddScoped<StandingsSyncService>();
builder.Services.AddScoped<CoreDataAutomationOrchestrator>();
builder.Services.AddScoped<CoreAutomationCatalogRefreshJobService>();
builder.Services.AddScoped<CoreAutomationTeamsRollingJobService>();
builder.Services.AddScoped<CoreAutomationStandingsRollingJobService>();
builder.Services.AddScoped<CoreAutomationFixturesRollingJobService>();
builder.Services.AddScoped<CoreAutomationOddsPreMatchJobService>();
builder.Services.AddScoped<CoreAutomationOddsLiveJobService>();
builder.Services.AddScoped<CoreAutomationRepairJobService>();
builder.Services.AddScoped<DataRetentionService>();
builder.Services.AddHostedService<CoreDataAutomationBackgroundService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();
builder.Services.AddHostedService<TheOddsViewerDrivenRefreshBackgroundService>();

// CORS
var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var hasConfiguredAllowedOrigins = allowedOrigins is { Length: > 0 };
var configuredAllowedOrigins = allowedOrigins ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (hasConfiguredAllowedOrigins)
        {
            policy.WithOrigins(configuredAllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (isDevelopment)
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Fail closed in non-development when explicit allowed origins are missing.
        }
    });
});

var app = builder.Build();

if (!isDevelopment && !hasConfiguredAllowedOrigins)
{
    app.Logger.LogWarning("CORS:AllowedOrigins is not configured outside development. Cross-origin browser requests will be blocked.");
}

if (!isDevelopment && string.IsNullOrWhiteSpace(jwtSigningKey))
{
    app.Logger.LogWarning("JwtAuth:SigningKey is not configured outside development. JWTs currently fall back to the API key secret; set a dedicated signing key for production.");
}

if (swaggerEnabled && !isDevelopment)
{
    app.Logger.LogWarning("Swagger is enabled outside development. Keep Swagger__Enabled disabled by default and only turn it on intentionally.");
}

// Global exception handler
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

// Swagger
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP pipeline
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapControllers();
var liveOddsHub = app.MapHub<LiveOddsHub>(LiveOddsHub.Route);
if (authEnabled)
{
    liveOddsHub.RequireAuthorization();
}
else
{
    liveOddsHub.AllowAnonymous();
}

// Public health check
app.MapGet("/ping", () => "pong").AllowAnonymous();

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

static bool LooksLikeJwt(string value)
{
    return value.Count(x => x == '.') == 2;
}
