using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginController> _logger;
    private readonly Dictionary<string, (string Password, string[] Roles)> _validUsers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["vilniaus"] = ("vandenys", ["Admin"]),
            ["pimpius"] = ("esi", []),
            ["luna"] = ("bot", []),
            ["scum"] = ("dayz", [])
        };

    public LoginController(ITokenService tokenService, ILogger<LoginController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (!ValidateCredentials(request.Username, request.Password))
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var roles = _validUsers[request.Username].Roles;

        var token = _tokenService.GenerateToken(request.Username, roles);

        return Ok(new LoginResponse(token, "Bearer"));
    }

    private bool ValidateCredentials(string username, string password)
    {
        if (_validUsers.TryGetValue(username, out var u))
        {
            if (password == u.Password) return true;
            else return false;
        }
        else return false;
    }
}

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string TokenType);