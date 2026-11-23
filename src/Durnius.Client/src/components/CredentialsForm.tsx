import { useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { errorMessages, login, register } from "../lib/api";
import {
  PASSWORD_MAX_LENGTH,
  USERNAME_MAX_LENGTH,
  validateCredentials,
} from "../lib/validation";
import { useAuth } from "../auth/AuthContext";
import FormErrors from "./FormErrors";

interface CredentialsFormProps {
  mode: "login" | "register";
}

/** Username + password form shared by the login and register pages. */
export default function CredentialsForm({ mode }: CredentialsFormProps) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const { login: storeToken } = useAuth();
  const navigate = useNavigate();

  const isLogin = mode === "login";

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const validationErrors = validateCredentials(username, password);
    if (validationErrors.length > 0) {
      setErrors(validationErrors);
      return;
    }

    setErrors([]);
    setSubmitting(true);
    try {
      const auth = isLogin
        ? await login(username, password)
        : await register(username, password);
      storeToken(auth.token);
      navigate("/lobby", { replace: true });
    } catch (err) {
      setErrors(errorMessages(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="auth-form">
      <div className="field">
        <label htmlFor="username">Username</label>
        <input
          id="username"
          className="input"
          type="text"
          value={username}
          onChange={(event) => {
            setUsername(event.target.value);
            setErrors([]);
          }}
          autoComplete="username"
          maxLength={USERNAME_MAX_LENGTH}
          autoFocus
        />
      </div>

      <div className="field">
        <label htmlFor="password">Password</label>
        <input
          id="password"
          className="input"
          type="password"
          value={password}
          onChange={(event) => {
            setPassword(event.target.value);
            setErrors([]);
          }}
          autoComplete={isLogin ? "current-password" : "new-password"}
          maxLength={PASSWORD_MAX_LENGTH}
        />
      </div>

      <FormErrors errors={errors} />

      <button
        type="submit"
        className="btn btn-primary btn-block"
        disabled={submitting}
      >
        {submitting
          ? isLogin
            ? "Logging in…"
            : "Creating account…"
          : isLogin
            ? "Log in"
            : "Create account"}
      </button>
    </form>
  );
}
