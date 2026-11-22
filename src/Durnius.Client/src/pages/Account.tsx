import { useState } from "react";
import type { FormEvent } from "react";
import { useAuth } from "../auth/AuthContext";
import FormErrors from "../components/FormErrors";
import GoldDisplay from "../components/GoldDisplay";
import { changePassword, errorMessages } from "../lib/api";
import { PASSWORD_MAX_LENGTH } from "../lib/validation";

export default function Account() {
  const { username, logout } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [errors, setErrors] = useState<string[]>([]);
  const [successMessage, setSuccessMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrors([]);
    setSuccessMessage("");

    const validationErrors: string[] = [];
    if (!currentPassword) {
      validationErrors.push("Current password is required.");
    }
    if (!newPassword) {
      validationErrors.push("New password is required.");
    } else if (newPassword.length > PASSWORD_MAX_LENGTH) {
      validationErrors.push(
        `Password must be at most ${PASSWORD_MAX_LENGTH} characters.`
      );
    }
    if (newPassword && newPassword !== confirmPassword) {
      validationErrors.push("New password confirmation does not match.");
    }

    if (validationErrors.length > 0) {
      setErrors(validationErrors);
      return;
    }

    setSubmitting(true);
    try {
      await changePassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setSuccessMessage("Your password has been changed successfully.");
    } catch (error) {
      setErrors(errorMessages(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Account</h1>
        <p className="muted">Manage your profile and password.</p>
      </div>

      <div className="account-grid">
        <section className="panel profile-panel">
          <span className="avatar avatar-lg" aria-hidden="true">
            {(username ?? "?").charAt(0).toUpperCase()}
          </span>
          <div className="profile-name">
            <strong>{username ?? "Player"}</strong>
            <span className="muted">Signed in</span>
          </div>
          <GoldDisplay large />
          <button type="button" className="btn" onClick={logout}>
            Log out
          </button>
        </section>

        <section className="panel">
          <h2>Change password</h2>
          <form onSubmit={handleSubmit} className="auth-form">
            <div className="field">
              <label htmlFor="current-password">Current password</label>
              <input
                id="current-password"
                className="input"
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                autoComplete="current-password"
                maxLength={PASSWORD_MAX_LENGTH}
              />
            </div>

            <div className="field">
              <label htmlFor="new-password">New password</label>
              <input
                id="new-password"
                className="input"
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                autoComplete="new-password"
                maxLength={PASSWORD_MAX_LENGTH}
              />
            </div>

            <div className="field">
              <label htmlFor="confirm-password">Confirm new password</label>
              <input
                id="confirm-password"
                className="input"
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                autoComplete="new-password"
                maxLength={PASSWORD_MAX_LENGTH}
              />
            </div>

            <FormErrors errors={errors} />
            {successMessage && (
              <div className="alert alert-success" role="status">
                {successMessage}
              </div>
            )}

            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting}
            >
              {submitting ? "Changing password…" : "Change password"}
            </button>
          </form>
        </section>
      </div>
    </>
  );
}
