using Durnius.Web.Data;
using Durnius.Web.Features.Leaderboard;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Durnius.Web.Tests;

public class LeaderboardPayoutTests
{
    [Fact]
    public async Task Settle_PaysTopPlayersCorrectly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<ILeaderboardService, LeaderboardService>()
            .AddLogging()
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var day = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

        var alice = new User { Username = "Alice", PasswordHash = "x" };
        var bob = new User { Username = "Bob", PasswordHash = "x" };
        var carol = new User { Username = "Carol", PasswordHash = "x" };
        db.Users.AddRange(alice, bob, carol);
        await db.SaveChangesAsync();

        db.MatchHistories.AddRange(
            new MatchHistory
            {
                GameType = "karas",
                MatchDate = day.AddHours(1),
                Participants = new List<MatchParticipant>
                {
                    new() { UserId = alice.Id, Placement = 1 },
                    new() { UserId = bob.Id, Placement = 2 }
                }
            },
            new MatchHistory
            {
                GameType = "karas",
                MatchDate = day.AddHours(2),
                Participants = new List<MatchParticipant>
                {
                    new() { UserId = alice.Id, Placement = 1 },
                    new() { UserId = carol.Id, Placement = 2 }
                }
            },
            new MatchHistory
            {
                GameType = "karas",
                MatchDate = day.AddHours(3),
                Participants = new List<MatchParticipant>
                {
                    new() { UserId = bob.Id, Placement = 1 },
                    new() { UserId = alice.Id, Placement = 2 }
                }
            });
        await db.SaveChangesAsync();

        var job = new DailyLeaderboardSettlementService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DailyLeaderboardSettlementService>.Instance);

        var paid = await job.SettleAsync(day, CancellationToken.None);

        Assert.Equal(2, paid);

        db.ChangeTracker.Clear();
        var gold = await db.Users.ToDictionaryAsync(user => user.Username, user => user.Gold);

        Assert.Equal(100, gold["Alice"]); // 2 wins -> rank 1
        Assert.Equal(90, gold["Bob"]);    // 1 win  -> rank 2
        Assert.Equal(0, gold["Carol"]);   // 0 wins -> not ranked
    }
}
