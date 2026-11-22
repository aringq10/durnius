import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export default function Lobby() {
  const navigate = useNavigate();
  const { logout } = useAuth();

  function handleLogout() {
    // JWT auth is stateless: logging out only means discarding the token.
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="card">
      <h1>Choose a game</h1>
      <p className="lobby-buttons">
        <button type="button" onClick={() => navigate("/karas")}>
          Join Karas
        </button>
        <button type="button" onClick={() => navigate("/durnius")}>
          Join Durnius
        </button>
      </p>
      <p className="links">
        <button type="button" onClick={() => navigate("/leaderboard")}>
          Leaderboard
        </button>
        <button type="button" onClick={handleLogout}>
          Log Out
        </button>
      </p>
    </div>
  );
}
