using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Lobby;

public class LobbyController : Controller
{
    [HttpGet("/lobby")]
    public IActionResult Index()
    {
        return View();
    }
}
