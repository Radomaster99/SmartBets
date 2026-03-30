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
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = exceptionHandlerPathFeature?.Error;

        await context.Response.WriteAsync(ex?.ToString() ?? "Unknown error");
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