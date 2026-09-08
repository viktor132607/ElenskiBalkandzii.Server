using ElenskiBalkandzii.Server.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();

app.MapGet("/api/health", async (ApplicationDbContext db) =>
{
    bool databaseAvailable = await db.Database.CanConnectAsync();
    return Results.Ok(new { status = "ok", database = databaseAvailable });
});

app.Run();
