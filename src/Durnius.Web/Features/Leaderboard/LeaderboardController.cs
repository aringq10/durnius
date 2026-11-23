using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Leaderboard;

[ApiController]
public class LeaderboardController(ILeaderboardService leaderboardService) : ControllerBase
{
    [HttpGet("/api/leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await leaderboardService.GetDailyAsync(cancellationToken));
    }
}
