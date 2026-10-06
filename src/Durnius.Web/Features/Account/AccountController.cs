using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Account;

[ApiController]
public class AccountController(IAntiforgery antiforgery) : ControllerBase
{
    private static readonly List<(string Username, string Password)> Users = new()
    {
        ("vilniaus", "vandenys")
    };

    private static readonly object UsersLock = new();

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login() => AntiforgeryToken();

    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest(new { error = "antiforgery_validation_failed" });
        }

        bool isValidUser;

        lock (UsersLock)
        {
            isValidUser = Users.Any(u => u.Username == model.Username && u.Password == model.Password);
        }

        if (!isValidUser)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        await SignInUserAsync(model.Username, model.RememberMe);

        return Ok(new
        {
            username = model.Username,
            authenticated = true,
            requestToken = CreateRequestToken()
        });
    }

    [HttpGet("/register")]
    [AllowAnonymous]
    public IActionResult Register() => AntiforgeryToken();

    [HttpPost("/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest(new { error = "antiforgery_validation_failed" });
        }

        lock (UsersLock)
        {
            if (Users.Any(u => u.Username == model.Username))
            {
                return Conflict(new { error = "Username already taken." });
            }

            Users.Add((model.Username, model.Password));
        }

        await SignInUserAsync(model.Username, isPersistent: false);

        return Created("/lobby", new
        {
            username = model.Username,
            authenticated = true,
            requestToken = CreateRequestToken()
        });
    }

    private IActionResult AntiforgeryToken() => Ok(new { requestToken = CreateRequestToken() });

    private string CreateRequestToken() => antiforgery.GetAndStoreTokens(HttpContext).RequestToken!;

    private async Task SignInUserAsync(string username, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username)
        };

        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : null
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest(new { error = "antiforgery_validation_failed" });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { authenticated = false });
    }
}
