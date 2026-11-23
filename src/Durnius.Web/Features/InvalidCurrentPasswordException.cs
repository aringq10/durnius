namespace Durnius.Web.Features.Account;

/// <summary>
/// Thrown when an authenticated user supplies an incorrect current password
/// while attempting to change their password.
/// </summary>
public sealed class InvalidCurrentPasswordException()
    : Exception("The current password is incorrect.")
{
}
