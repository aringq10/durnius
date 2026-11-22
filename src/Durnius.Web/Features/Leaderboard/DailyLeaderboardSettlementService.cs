using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Leaderboard;

public sealed class DailyLeaderboardSettlementService(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyLeaderboardSettlementService> logger) : BackgroundService
{
    private static readonly TimeSpan AfterMidnight = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Deliberately no startup catch-up: with no persisted marker we cannot
        // know whether a previous process already paid for yesterday.
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextMidnight = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
            var runAt = nextMidnight + AfterMidnight;

            logger.LogInformation("Leaderboard payout scheduled for {RunAt:u}", runAt);

            await Task.Delay(runAt - now, stoppingToken);

            var periodStartUtc = DateTime.SpecifyKind(
                DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc);

            try
            {
                await SettleAsync(periodStartUtc, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "Leaderboard payout failed for {PeriodStartUtc:d}", periodStartUtc);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task SettleAsync(
        DateTime periodStartUtc,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var leaderboard = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entries = await leaderboard.GetStandingsAsync(
            periodStartUtc, LeaderboardService.EntryCount, cancellationToken);

        if (entries.Count == 0)
        {
            logger.LogInformation(
                "No leaderboard entries for {PeriodStartUtc:d}; nothing to pay.", periodStartUtc);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var entry in entries)
        {
            var gold = LeaderboardService.GoldForRank(entry.Rank);

            await db.Users
                .Where(user => user.Id == entry.UserId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        user => user.Gold, user => user.Gold + gold),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Paid {Count} leaderboard entries for {PeriodStartUtc:d}",
            entries.Count, periodStartUtc);
    }
}
