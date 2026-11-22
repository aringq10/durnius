using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Durnius.Web.Common;
using Durnius.Web.Data;

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

        if (result.Error == AccountError.InvalidCredentials)
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

    private IActionResult ToActionResult(ServiceResult<User, AccountError> result)
    {
        return result.Match<IActionResult>(
            user => Ok(new AuthResponse(_tokenService.GenerateToken(user), "Bearer")),
            (error, messages) =>
            {
                var body = new { errors = messages };
                return error switch
                {
                    AccountError.ValidationFailed => BadRequest(body),
                    AccountError.InvalidCredentials => Unauthorized(body),
                    AccountError.UsernameTaken => Conflict(body),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                };
            });
    }
}

public record LoginRequest(string? Username, string? Password);

public record RegisterRequest(string? Username, string? Password);

public record AuthResponse(string Token, string TokenType);
