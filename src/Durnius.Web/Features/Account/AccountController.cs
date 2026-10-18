using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Durnius.Web.Features.Account;

[ApiController]
public class AccountController(
    IAccountService accountService,
    ITokenService tokenService,
    ILogger<AccountController> logger) : ControllerBase
{
    private readonly IAccountService _accountService = accountService;
    private readonly ITokenService _tokenService = tokenService;
    private readonly ILogger<AccountController> _logger = logger;

    [HttpPost("/api/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _accountService.LoginAsync(request.Username, request.Password);

        if (result.Status == AccountResultStatus.InvalidCredentials)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
        }

        return ToActionResult(result);
    }

    [HttpPost("/api/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _accountService.RegisterAsync(request.Username, request.Password);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(AccountResult result)
    {
        if (!result.Succeeded)
        {
            var body = new { errors = result.Errors };

            return result.Status switch
            {
                AccountResultStatus.ValidationFailed => BadRequest(body),
                AccountResultStatus.InvalidCredentials => Unauthorized(body),
                AccountResultStatus.UsernameTaken => Conflict(body),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        var token = _tokenService.GenerateToken(result.User!);

        return Ok(new AuthResponse(token, "Bearer"));
    }
}

public record LoginRequest(string? Username, string? Password);

public record RegisterRequest(string? Username, string? Password);

public record AuthResponse(string Token, string TokenType);
