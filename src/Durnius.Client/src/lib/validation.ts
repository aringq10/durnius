// Mirrors Durnius.Web/Features/Account/AccountService.ValidateInput so the
// client rejects the same input as the API, with the same messages and limits.
// Keep these in sync with AccountService.UsernameMaxLength / PasswordMaxLength.
export const USERNAME_MAX_LENGTH = 32;
export const PASSWORD_MAX_LENGTH = 72;

export function validateCredentials(
  username: string,
  password: string
): string[] {
  const errors: string[] = [];

  if (username.trim().length === 0) {
    errors.push("Username is required.");
  } else if (username.length > USERNAME_MAX_LENGTH) {
    errors.push(`Username must be at most ${USERNAME_MAX_LENGTH} characters.`);
  }

  if (password.length === 0) {
    errors.push("Password is required.");
  } else if (password.length > PASSWORD_MAX_LENGTH) {
    errors.push(`Password must be at most ${PASSWORD_MAX_LENGTH} characters.`);
  }

  return errors;
}
