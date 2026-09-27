using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Lobby;

[Route("lobby")]
public class LobbyController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}