using Durnius.Web.Data;
using Microsoft.EntityFrameworkCore;
using Durnius.Web.Common;

namespace Durnius.Web.Features.Account;

public interface IAccountService
{
    IReadOnlyList<string> ValidateInput(string? username, string? password);
    Task<ServiceResult<User, AccountError>> LoginAsync(
        string? username, string? password);
    Task<ServiceResult<User, AccountError>> RegisterAsync(
        string? username, string? password);
}

public enum AccountError
{
    ValidationFailed,
    InvalidCredentials,
    UsernameTaken
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

    public async Task<ServiceResult<User, AccountError>> LoginAsync(
        string? username, string? password)
    {
        var errors = ValidateInput(username, password);
        if (errors.Count > 0)
        {
            return ServiceResult<User, AccountError>.Failure(
                AccountError.ValidationFailed, errors);
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password!, user.PasswordHash))
        {
            return ServiceResult<User, AccountError>.Failure(
                AccountError.InvalidCredentials,
                ["Invalid username or password."]);
        }

        return ServiceResult<User, AccountError>.Success(user);
    }

    public async Task<ServiceResult<User, AccountError>> RegisterAsync(
        string? username, string? password)
    {
        var errors = ValidateInput(username, password);
        if (errors.Count > 0)
        {
            return ServiceResult<User, AccountError>.Failure(
                AccountError.ValidationFailed, errors);
        }

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            return ServiceResult<User, AccountError>.Failure(
                AccountError.UsernameTaken,
                ["Username already taken."]);
        }

        var user = new User
        {
            Username = username!,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password!)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return ServiceResult<User, AccountError>.Success(user);
    }
}