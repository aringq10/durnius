import { useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import FormErrors from "../components/FormErrors";
import { errorMessages, putJson } from "../lib/api";
import { PASSWORD_MAX_LENGTH } from "../lib/validation";

export default function Account() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [errors, setErrors] = useState<string[]>([]);
  const [successMessage, setSuccessMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const { token } = useAuth();
  const navigate = useNavigate();

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
      await putJson<void>(
        "/api/account/password",
        { currentPassword, newPassword },
        token
      );
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
    <main className="card account-card">
      <button type="button" className="account-back" onClick={() => navigate("/lobby")}>
        ← Back to lobby
      </button>
      <h1>Account settings</h1>
      <p>Change your password to keep your account secure.</p>

      <form onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="current-password">Current password</label>
          <input
            id="current-password"
            type="password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            autoComplete="current-password"
          />
        </div>

        <div className="field">
          <label htmlFor="new-password">New password</label>
          <input
            id="new-password"
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
            type="password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            autoComplete="new-password"
            maxLength={PASSWORD_MAX_LENGTH}
          />
        </div>

        <FormErrors errors={errors} />
        {successMessage && (
          <p className="success-message" role="status">
            {successMessage}
          </p>
        )}

        <button type="submit" disabled={submitting}>
          {submitting ? "Changing password..." : "Change password"}
        </button>
      </form>
    </main>
  );
}