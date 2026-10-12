using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Lobby;

[ApiController]
[Authorize]
public class LobbyController : ControllerBase
{
    // Hardcoded games until real game data exists. Public games live in the
    // order players see them in the lobby.
    private static readonly IReadOnlyList<string> Games = ["durnius", "karas", "lagaminas"];

    [HttpGet("/api/lobby")]
    public IActionResult Index()
    {
        return Ok(Games);
    }
}