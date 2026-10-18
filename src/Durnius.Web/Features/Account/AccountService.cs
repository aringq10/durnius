using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Features.Account;

public interface IAccountService
{
    IReadOnlyList<string> ValidateInput(string? username, string? password);
    Task<AccountResult> LoginAsync(string? username, string? password);
    Task<AccountResult> RegisterAsync(string? username, string? password);
}

public enum AccountResultStatus
{
    Success,
    ValidationFailed,
    InvalidCredentials,
    UsernameTaken
}

public sealed record AccountResult(
    AccountResultStatus Status,
    User? User,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Status == AccountResultStatus.Success;
}

public class AccountService(AppDbContext db) : IAccountService
{
    // Mirrors the constraints configured on the User entity / Users table.
    public const int UsernameMaxLength = 32;
    public const int PasswordMaxLength = 72; // BCrypt only uses the first 72 bytes.

    private readonly AppDbContext _db = db;

    public IReadOnlyList<string> ValidateInput(string? username, string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add("Username is required.");
        }
        else if (username.Length > UsernameMaxLength)
        {
            errors.Add($"Username must be at most {UsernameMaxLength} characters.");
        }

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
        }
        else if (password.Length > PasswordMaxLength)
        {
            errors.Add($"Password must be at most {PasswordMaxLength} characters.");
        }

        return errors;
    }

    public async Task<AccountResult> LoginAsync(string? username, string? password)
    {
        var errors = ValidateInput(username, password);
        if (errors.Count > 0)
        {
            return new AccountResult(AccountResultStatus.ValidationFailed, null, errors);
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password!, user.PasswordHash))
        {
            return new AccountResult(
                AccountResultStatus.InvalidCredentials,
                null,
                ["Invalid username or password."]);
        }

        return new AccountResult(AccountResultStatus.Success, user, []);
    }

    public async Task<AccountResult> RegisterAsync(string? username, string? password)
    {
        var errors = ValidateInput(username, password);
        if (errors.Count > 0)
        {
            return new AccountResult(AccountResultStatus.ValidationFailed, null, errors);
        }

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            return new AccountResult(
                AccountResultStatus.UsernameTaken,
                null,
                ["Username already taken."]);
        }

        var user = new User
        {
            Username = username!,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password!)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AccountResult(AccountResultStatus.Success, user, []);
    }
}
