using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly string[] _games = [
        "durnius", "karas", "asilas"
    ];

    [HttpGet]
    public string[] Get()
    {
        return _games;
    }
}
