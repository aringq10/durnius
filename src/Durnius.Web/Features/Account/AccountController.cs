using System.Security.Claims;
using Durnius.Web.Common;
using Durnius.Web.Data;
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

        if (result.Error == AccountError.InvalidCredentials)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
        }

        return ToActionResult(result);
    }
    [HttpPut("/api/account/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _accountService.ChangePasswordAsync(
                userId, request.CurrentPassword, request.NewPassword);

            return result.Match<IActionResult>(
                _ => NoContent(),
                (error, messages) => error switch
                {
                    AccountError.ValidationFailed => BadRequest(new { errors = messages }),
                    AccountError.UserNotFound => NotFound(new { errors = messages }),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                });
        }
        catch (InvalidCurrentPasswordException exception)
        {
            _logger.LogWarning(
                "User {UserId} supplied an incorrect current password while changing it.",
                userId);

            return BadRequest(new { errors = new[] { exception.Message } });
        }
    }


    [HttpPost("/api/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _accountService.RegisterAsync(request.Username, request.Password);

        return ToActionResult(result);
    }

    [HttpGet("/api/account/me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var account = await _accountService.GetCurrentAsync(username, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpGet("/api/account/matches")]
    [Authorize]
    public async Task<IActionResult> MatchHistory(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var entries = await _accountService.GetMatchHistoryAsync(userId, cancellationToken);
        return Ok(entries);
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

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public record AuthResponse(string Token, string TokenType);
