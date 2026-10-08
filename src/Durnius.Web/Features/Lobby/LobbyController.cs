using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Lobby;

[ApiController]
public class LobbyController : ControllerBase
{
    [HttpGet("/lobby")]
    public IActionResult GetLobby()
    {
        return Ok(new { username = User.Identity?.Name });
    }
}
