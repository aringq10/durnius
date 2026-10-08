using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Durnius.Web.Features.Account;

public sealed class LoginRequest
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public sealed class RegisterRequest
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;
}

public enum AccountErrorCode
{
    InvalidCredentials,
    UsernameAlreadyTaken
}

public sealed record AccountErrorResponse(string Error)
{
    public static AccountErrorResponse From(AccountErrorCode code) =>
        new(JsonNamingPolicy.CamelCase.ConvertName(code.ToString()));
}