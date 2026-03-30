using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<FootballApiService>();

builder.Services.AddScoped<CountrySyncService>();
builder.Services.AddScoped<LeagueSyncService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<FixtureSyncService>();
builder.Services.AddScoped<BookmakerSyncService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<SyncStateService>();
builder.Services.AddScoped<PreloadSyncService>();

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

// да се изтрие надолу

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";

        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var ex = exceptionHandlerPathFeature?.Error;

        await context.Response.WriteAsync(ex?.ToString() ?? "Unknown error");
    });
});

// до тук

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://*:{port}");

app.UseSwagger();
app.UseSwaggerUI();


app.UseCors("FrontendPolicy");


app.UseAuthorization();
app.MapControllers();

app.MapGet("/ping", () => "pong");
app.Run();