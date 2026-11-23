using Durnius.Web.Data;
using Durnius.Web.Features.Account;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Durnius.Web.Tests.Features.Account;

/// <summary>Service-level integration tests against a real (SQLite) database.</summary>
public class AccountServiceTests
{
    private sealed class Fixture : IDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required AccountService Service { get; init; }

        public void Dispose()
        {
            Db.Dispose();
            Connection.Dispose();
        }
    }

    private static Fixture NewService()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return new Fixture { Connection = connection, Db = db, Service = new AccountService(db) };
    }

    [Fact]
    public void ValidateInputChecksLimitsAndReturnsAllErrors()
    {
        using var fixture = NewService();
        var service = fixture.Service;

        Assert.Empty(service.ValidateInput("Alice", "password"));
        Assert.Empty(service.ValidateInput(new string('a', 32), new string('p', 72)));

        Assert.Equal(
            ["Username is required.", "Password is required."],
            service.ValidateInput("  ", ""));
        Assert.Equal(
            ["Username must be at most 32 characters.", "Password must be at most 72 characters."],
            service.ValidateInput(new string('a', 33), new string('p', 73)));
    }

    [Fact]
    public async Task RegisterHashesThePasswordAndLoginVerifiesIt()
    {
        using var fixture = NewService();
        var (db, service) = (fixture.Db, fixture.Service);

        var registration = await service.RegisterAsync("Alice", "correct horse battery staple");

        Assert.True(registration.Succeeded);
        Assert.NotNull(registration.Value);
        Assert.NotEqual("correct horse battery staple", registration.Value.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery staple", registration.Value.PasswordHash));

        var duplicate = await service.RegisterAsync("Alice", "another-password");
        Assert.Equal(AccountError.UsernameTaken, duplicate.Error);
        Assert.Equal(["Username already taken."], duplicate.Messages);
        Assert.Equal(1, await db.Users.CountAsync());

        Assert.Equal("Alice", (await service.LoginAsync("Alice", "correct horse battery staple")).Value?.Username);
        Assert.Equal(AccountError.InvalidCredentials, (await service.LoginAsync("Alice", "wrong")).Error);
        Assert.Equal(AccountError.InvalidCredentials, (await service.LoginAsync("Ghost", "password")).Error);
    }

    [Fact]
    public async Task ChangePasswordEnforcesCurrentPasswordAndRejectsReuse()
    {
        using var fixture = NewService();
        var service = fixture.Service;
        var registered = await service.RegisterAsync("Alice", "original");
        var userId = registered.Value!.Id;

        Assert.Throws<InvalidCurrentPasswordException>(() =>
            service.ChangePasswordAsync(userId, "wrong", "new").GetAwaiter().GetResult());

        var reused = await service.ChangePasswordAsync(userId, "original", "original");
        Assert.Equal(AccountError.ValidationFailed, reused.Error);
        Assert.Equal(
            ["The new password must be different from the current password."],
            reused.Messages);

        var invalid = await service.ChangePasswordAsync(userId, "", "");
        Assert.Equal(AccountError.ValidationFailed, invalid.Error);
        Assert.Equal(2, invalid.Messages.Count);

        var missing = await service.ChangePasswordAsync(999, "original", "new");
        Assert.Equal(AccountError.UserNotFound, missing.Error);

        var changed = await service.ChangePasswordAsync(userId, "original", "new-password");
        Assert.True(changed.Value);
        Assert.Equal(AccountError.InvalidCredentials, (await service.LoginAsync("Alice", "original")).Error);
        Assert.Equal("Alice", (await service.LoginAsync("Alice", "new-password")).Value?.Username);
    }

    [Fact]
    public async Task GetMatchHistoryReturnsTenMostRecentEntriesForThatUserOnly()
    {
        using var fixture = NewService();
        var (db, service) = (fixture.Db, fixture.Service);
        var alice = (await service.RegisterAsync("Alice", "password")).Value!;
        var bob = (await service.RegisterAsync("Bob", "password")).Value!;
        var day = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 12; i++)
        {
            db.MatchHistories.Add(new MatchHistory
            {
                GameType = i % 2 == 0 ? "karas" : "durnius",
                MatchDate = day.AddMinutes(i),
                Participants = [new MatchParticipant { UserId = alice.Id, Placement = i % 2 + 1 }]
            });
        }

        db.MatchHistories.Add(new MatchHistory
        {
            GameType = "karas",
            MatchDate = day.AddDays(1),
            Participants = [new MatchParticipant { UserId = bob.Id, Placement = 1 }]
        });
        await db.SaveChangesAsync();

        var history = await service.GetMatchHistoryAsync(alice.Id);

        Assert.Equal(10, history.Count);
        Assert.Equal("durnius", history[0].GameType);
        Assert.Equal(2, history[0].Placement);
        Assert.Equal("karas", history[^1].GameType);
        Assert.Equal(1, history[^1].Placement);
        Assert.DoesNotContain(history, entry => entry.MatchDateUtc == day.AddDays(1));
    }
}
