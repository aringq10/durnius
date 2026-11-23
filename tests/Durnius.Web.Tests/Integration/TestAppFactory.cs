using Durnius.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Durnius.Web.Tests.Integration;

/// <summary>
/// Boots the real ASP.NET pipeline against an in-memory SQLite database.
/// Program.cs migrates at startup, so the provider must be replaced before
/// the host is built; JWT settings are supplied because appsettings.json
/// intentionally does not contain a key.
/// </summary>
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "test-key-that-is-long-enough-for-hs256-signing";
    public const string JwtIssuer = "durnius-tests";
    public const string JwtAudience = "durnius-tests-client";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    // Top-level statements in Program.cs read Jwt:* while the host is being
    // created, before WebApplicationFactory callbacks run. Environment
    // variables are visible to WebApplication.CreateBuilder at that point.
    static TestAppFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
    }

    public TestAppFactory()
    {
        // Keep the shared in-memory database alive for the whole host.
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove both the Npgsql provider services and the options
            // configuration that Program.cs registered, otherwise EF sees two
            // providers in the same service provider.
            foreach (var descriptor in services.ToList())
            {
                var serviceType = descriptor.ServiceType;
                var implementationType = descriptor.ImplementationType;
                if (serviceType == typeof(DbContextOptions<AppDbContext>) ||
                    serviceType == typeof(DbContextOptions) ||
                    serviceType == typeof(IDbContextOptionsConfiguration<AppDbContext>) ||
                    serviceType.Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true ||
                    implementationType?.Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true)
                {
                    services.Remove(descriptor);
                }
            }

            services.AddDbContext<AppDbContext>(options => options
                .UseSqlite(_connection)
                // The checked-in migrations predate a model tweak; startup must
                // still work in tests without adding a production migration.
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
