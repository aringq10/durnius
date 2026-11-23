using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Leaderboard;

public interface ILeaderboardService
{
    Task<LeaderboardResponse> GetDailyAsync(CancellationToken cancellationToken = default);
}

public sealed record LeaderboardEntry(int Rank, int UserId, string Username, int Wins);


public sealed record LeaderboardResponse(
    DateTime PeriodStartUtc,
    DateTime ResetAtUtc,
    IReadOnlyList<LeaderboardEntry> Entries);

public sealed class LeaderboardService(AppDbContext db) : ILeaderboardService
{
    public const int EntryCount = 10;

    private readonly AppDbContext _db = db;

    public async Task<LeaderboardResponse> GetDailyAsync(
        CancellationToken cancellationToken = default)
    {
        var periodStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(1);

        var top = await _db.MatchParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.Placement == 1 &&
                participant.MatchHistory.MatchDate >= periodStartUtc &&
                participant.MatchHistory.MatchDate < periodEndUtc)
            .GroupBy(participant => new
            {
                participant.User.Id,
                participant.User.Username
            })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Username,
                Wins = group.Count()
            })
            .OrderByDescending(row => row.Wins)
            .ThenBy(row => row.Username)
            .ThenBy(row => row.Id)
            .Take(EntryCount)
            .ToListAsync(cancellationToken);

        var entries = top
            .Select((row, index) => new LeaderboardEntry(
                index + 1, row.Id, row.Username, row.Wins))
            .ToArray();

        return new LeaderboardResponse(periodStartUtc, periodEndUtc, entries);
    }
}
