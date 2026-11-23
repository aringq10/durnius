import { useEffect } from "react";
import { Link, Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { clearSessionExpired, isSessionExpired } from "../lib/auth";
import CredentialsForm from "../components/CredentialsForm";

export default function Login() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const expired =
    (location.state as { expired?: boolean } | null)?.expired === true ||
    isSessionExpired();

  // The notice has been picked up; a later manual logout must not show it.
  useEffect(() => {
    clearSessionExpired();
  }, []);

  if (isAuthenticated) {
    return <Navigate to="/lobby" replace />;
  }

  return (
    <div className="auth-wrap">
      <div className="auth-brand">
        <span className="brand-suit" aria-hidden="true">
          ♠
        </span>
        Durnius
      </div>
      <div className="panel auth-panel">
        <h1>Welcome back</h1>
        <p className="muted">Log in to take a seat at the table.</p>
        {expired && (
          <div className="alert alert-info" role="status">
            Your session has expired. Please log in again.
          </div>
        )}
        <CredentialsForm mode="login" />
        <p className="auth-alt">
          No account yet? <Link to="/register">Register</Link>
        </p>
      </div>
    </div>
  );
}
