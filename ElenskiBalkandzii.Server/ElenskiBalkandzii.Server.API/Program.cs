using ElenskiBalkandzii.Server.Data;
using ElenskiBalkandzii.Server.API;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<AdminAccess>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

string[] configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

string[] allowedOrigins = configuredOrigins.Length > 0
    ? configuredOrigins
    : builder.Environment.IsDevelopment()
        ? ["http://localhost:3000", "http://127.0.0.1:3000"]
        : ["https://elenskibalkandzii-client.onrender.com"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

app.UseCors("Client");
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();

using (IServiceScope scope = app.Services.CreateScope())
{
    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS site_content (id integer PRIMARY KEY, json jsonb NOT NULL, updated_at timestamp with time zone NOT NULL)");
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS site_images (id uuid PRIMARY KEY, content_type text NOT NULL, data bytea NOT NULL, created_at timestamp with time zone NOT NULL)");
}

app.MapGet("/api/health", async (ApplicationDbContext db) =>
{
    bool databaseAvailable = await db.Database.CanConnectAsync();
    return Results.Ok(new { status = "ok", database = databaseAvailable });
});

app.Run();
