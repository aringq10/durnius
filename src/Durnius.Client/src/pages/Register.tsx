import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { errorMessage, postJson } from "../lib/api";
import type { AuthResponse } from "../lib/api";
import { useAuth } from "../auth/AuthContext";

export default function Register() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const { storeToken } = useAuth();
  const navigate = useNavigate();

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const auth = await postJson<AuthResponse>("/register", {
        username,
        password,
      });
      storeToken(auth.token);
      navigate("/lobby", { replace: true });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSubmitting(false);
    }
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
            onChange={(e) => setUsername(e.target.value)}
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
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
          />
        </div>

        {error && (
          <div className="error" role="alert">
            {error}
          </div>
        )}

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
