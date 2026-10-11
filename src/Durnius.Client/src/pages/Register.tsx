import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { errorMessages, postJson } from "../lib/api";
import type { AuthResponse } from "../lib/api";
import { validateCredentials } from "../lib/validation";
import { useAuth } from "../auth/AuthContext";
import FormErrors from "../components/FormErrors";

export default function Register() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const { storeToken } = useAuth();
  const navigate = useNavigate();

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
      const auth = await postJson<AuthResponse>("/register", {
        username,
        password,
      });
      storeToken(auth.token);
      navigate("/lobby", { replace: true });
    } catch (err) {
      setErrors(errorMessages(err));
    } finally {
      setSubmitting(false);
    }
  }

  function handleUsernameChange(value: string) {
    setUsername(value);
    setErrors([]);
  }

  function handlePasswordChange(value: string) {
    setPassword(value);
    setErrors([]);
  }

  return (
    <div className="card">
      <h2>Register</h2>

      <form onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="username">Username</label>
          <br />
          <input
            id="username"
            type="text"
            value={username}
            onChange={(e) => handleUsernameChange(e.target.value)}
            autoComplete="username"
          />
        </div>

        <div className="field">
          <label htmlFor="password">Password</label>
          <br />
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => handlePasswordChange(e.target.value)}
            autoComplete="new-password"
          />
        </div>

        <FormErrors errors={errors} />

        <button type="submit" disabled={submitting}>
          {submitting ? "Registering..." : "Register"}
        </button>
      </form>

      <p className="links">
        Already have an account? <Link to="/login">Log In</Link>
      </p>
    </div>
  );
}
