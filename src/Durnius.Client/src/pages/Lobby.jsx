import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getJson } from "../lib/api.js";
import { useAuth } from "../auth/AuthContext.jsx";

export default function Lobby() {
  const { token, logout } = useAuth();
  const navigate = useNavigate();
  const [games, setGames] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getJson("/lobby", token)
      .then((body) => {
        if (!cancelled) setGames(body);
      })
      .catch((err) => {
        if (cancelled) return;
        if (err.status === 401) {
          // Token missing, expired, or rejected: back to the login page.
          logout();
          navigate("/login", { replace: true });
          return;
        }
        setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, logout, navigate]);

  function handleLogout() {
    // JWT auth is stateless: logging out only means discarding the token.
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <>
      <h1>Lobby</h1>
      <p>Welcome to the lobby!</p>

      <h3>Games</h3>
      {loading && <p>Loading games...</p>}
      {error && <p className="error">{error}</p>}
      {!loading && !error && (
        <ul>
          {games.map((game) => (
            <li key={game}>{game}</li>
          ))}
        </ul>
      )}

      <button type="button" onClick={handleLogout}>
        Log Out
      </button>
    </>
  );
}