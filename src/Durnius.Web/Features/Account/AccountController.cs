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

    [HttpGet("/antiforgerytoken")]
    [AllowAnonymous]
    public IActionResult GetAntiforgeryToken() => Ok(new { requestToken = CreateRequestToken() });

    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        bool isValidUser;

        lock (UsersLock)
        {
            isValidUser = Users.Any(u => u.Username == request.Username && u.Password == request.Password);
        }

        if (!isValidUser)
        {
            return Unauthorized(AccountErrorResponse.From(AccountErrorCode.InvalidCredentials));
        }

        await SignInUserAsync(request.Username, request.RememberMe);

        return Ok(new
        {
            username = request.Username,
            requestToken = CreateRequestToken()
        });
    }

    [HttpPost("/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        lock (UsersLock)
        {
            if (Users.Any(u => u.Username == request.Username))
            {
                return Conflict(AccountErrorResponse.From(AccountErrorCode.UsernameAlreadyTaken));
            }

            Users.Add((request.Username, request.Password));
        }

        await SignInUserAsync(request.Username, isPersistent: false);

        return Created("/lobby", new
        {
            username = request.Username,
            requestToken = CreateRequestToken()
        });
    }

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
        HttpContext.User = claimsPrincipal;
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        return Ok(new { requestToken = CreateRequestToken() });
    }
}
