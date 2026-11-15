using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Multiplayer;

public sealed record MatchPlacement(MatchPlayer Player, int Placement);

/// <summary>
/// Persists a finished match and one <see cref="MatchParticipant"/> per player.
/// The number of placements depends on the game, so games with more than two
/// players (up to eight) can use the same path as Karas.
/// </summary>
public class MatchHistoryRecorder(AppDbContext db, ILogger<MatchHistoryRecorder> logger)
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<MatchHistoryRecorder> _logger = logger;

    public async Task RecordAsync(Game game, IReadOnlyList<MatchPlacement> placements)
    {
        var usernames = placements
            .Select(placement => placement.Player.Username)
            .Distinct()
            .ToArray();

        var users = await _db.Users
            .Where(user => usernames.Contains(user.Username))
            .ToDictionaryAsync(user => user.Username);

        var participants = new List<MatchParticipant>();
        foreach (var placement in placements)
        {
            if (!users.TryGetValue(placement.Player.Username, out var user))
            {
                _logger.LogWarning(
                    "Cannot store match participant {Username}: user not found.",
                    placement.Player.Username);
                continue;
            }

            participants.Add(new MatchParticipant
            {
                UserId = user.Id,
                Placement = placement.Placement
            });
        }

        if (participants.Count == 0)
        {
            _logger.LogWarning("Skipping {Game} match history: no known participants.", game);
            return;
        }

        _db.MatchHistories.Add(new MatchHistory
        {
            GameType = game.ToString(),
            MatchDate = DateTime.UtcNow,
            MatchResult = participants
                .Where(participant => participant.Placement == 1)
                .Select(participant => (int?)participant.UserId)
                .FirstOrDefault(),
            Participants = participants
        });

        await _db.SaveChangesAsync();
    }
}
