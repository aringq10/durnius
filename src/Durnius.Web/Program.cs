using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Durnius.Web.Data;
using Durnius.Web.Features.Account;
using Durnius.Web.Features.Multiplayer;

// Default port the Vite dev server listens on.
const string DevClientOrigin = "http://localhost:5173";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer is not configured."),
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("Jwt:Audience is not configured."),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero // Remove the default 5-minute tolerance.
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAccountService, AccountService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("DevClient", policy =>
            policy.WithOrigins(DevClientOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod()));
}

builder.Services.AddSingleton<LobbyManager>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

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

app.Run();
