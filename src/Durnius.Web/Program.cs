using Durnius.Web.Data;
using Durnius.Web.Features.Account;
using Durnius.Web.Features.Multiplayer;
using Microsoft.EntityFrameworkCore;

const string DevClientOrigin = "http://localhost:5173";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

builder.Services
    .AddJwtAuthentication(builder.Configuration)
    .AddAccountServices()
    .AddMultiplayer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("DevClient", policy =>
            policy.WithOrigins(DevClientOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevClient");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LobbyHub>("/hubs/lobby");

app.MapFallbackToFile("index.html");

app.Run();
