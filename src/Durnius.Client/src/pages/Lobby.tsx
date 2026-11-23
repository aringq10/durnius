import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import LeaderboardPanel from "../components/LeaderboardPanel";

export default function Lobby() {
  const { username } = useAuth();

  return (
    <>
      <div className="page-head">
        <h1>Welcome back{username ? `, ${username}` : ""}.</h1>
        <p className="muted">
          Pick a game — you'll be matched with the next player in the queue.
        </p>
      </div>

      <div className="game-grid">
        <Link to="/karas" className="game-tile">
          <span className="game-glyph game-glyph-red" aria-hidden="true">
            ♥
          </span>
          <span className="game-tile-body">
            <span className="game-tile-title">Karas</span>
            <span className="game-tile-sub">
              The classic game of War. Red suits against black — draw, compare,
              and win the whole deck. Ties mean war.
            </span>
            <span className="game-tile-meta">2 players · auto-match</span>
          </span>
          <span className="game-tile-cta" aria-hidden="true">
            Play →
          </span>
        </Link>

        <Link to="/durnius" className="game-tile">
          <span className="game-glyph" aria-hidden="true">
            ♠
          </span>
          <span className="game-tile-body">
            <span className="game-tile-title">Durnius</span>
            <span className="game-tile-sub">
              Attack and defend, durak-style. Beat every card on the table —
              or pick it all up. Last one holding cards is the fool.
            </span>
            <span className="game-tile-meta">2 players · auto-match</span>
          </span>
          <span className="game-tile-cta" aria-hidden="true">
            Play →
          </span>
        </Link>
      </div>

      <LeaderboardPanel />
    </>
  );
}
