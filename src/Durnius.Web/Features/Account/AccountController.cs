using Durnius.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Account;

[ApiController]
public class AccountController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AppDbContext db, ITokenService tokenService, ILogger<AccountController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse(token, "Bearer"));
    }

    [HttpPost("/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return Conflict(new { message = "Username already taken." });
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse(token, "Bearer"));
    }
}

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Username, string Password);

public record AuthResponse(string Token, string TokenType);
