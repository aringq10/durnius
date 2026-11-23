import { Link, Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import CredentialsForm from "../components/CredentialsForm";

export default function Register() {
  const { isAuthenticated } = useAuth();

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
        <h1>Create your account</h1>
        <p className="muted">Pick a name and a password — that's all it takes.</p>
        <CredentialsForm mode="register" />
        <p className="auth-alt">
          Already have an account? <Link to="/login">Log in</Link>
        </p>
      </div>
    </div>
  );
}
