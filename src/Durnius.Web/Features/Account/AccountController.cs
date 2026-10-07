using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Account;

public class AccountController : Controller
{

    private readonly AppDbContext _db;
    
    public AccountController(AppDbContext db)
    {
        _db = db;
    }


    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (RedirectIfAuthenticated() is { } redirect)
        {
            return redirect;
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (RedirectIfAuthenticated() is { } redirect)
        {
            return redirect;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == model.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await SignInUserAsync(user.Username, model.RememberMe);


        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Lobby");
    }

    [HttpGet("/register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (RedirectIfAuthenticated() is { } redirect)
        {
            return redirect;
        }

        return View(new RegisterViewModel());
    }

    [HttpPost("/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (RedirectIfAuthenticated() is { } redirect)
        {
            return redirect;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

            
        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username already taken.");
            return View(model);
        }

        var user = new User
        {
            Username = model.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInUserAsync(model.Username, isPersistent: false);

        return RedirectToAction("Index", "Lobby");
    }



    private IActionResult? RedirectIfAuthenticated()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Lobby")
            : null;
    }

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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }
}
