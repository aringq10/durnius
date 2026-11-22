import { useCallback, useEffect, useState } from "react";
import { errorMessage, getLeaderboard } from "../lib/api";
import type { LeaderboardResponse } from "../lib/api";

const RETRY_MS = 30_000;

export default function Leaderboard() {
  const [board, setBoard] = useState<LeaderboardResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [retryTick, setRetryTick] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getLeaderboard();
      setBoard(data);
      setError(null);
    } catch (err: unknown) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, retryTick]);

  useEffect(() => {
    if (!error) return;
    const timer = window.setTimeout(() => setRetryTick((tick) => tick + 1), RETRY_MS);
    return () => window.clearTimeout(timer);
  }, [error]);

  useEffect(() => {
    if (!board || loading) return;
    const delay = Math.max(new Date(board.resetAtUtc).getTime() - Date.now(), 0) + 1000;
    const timer = window.setTimeout(() => void load(), delay);
    return () => window.clearTimeout(timer);
  }, [board, loading, load]);

  const resetTime = board
    ? new Date(board.resetAtUtc).toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        timeZone: "UTC",
      })
    : null;

  return (
    <div className="card leaderboard">
      <h1>Daily Leaderboard</h1>
      <p className="leaderboard-note">
        Most wins today (UTC)
        {resetTime ? ` · resets at ${resetTime} UTC` : ""}
      </p>

      {loading && !board && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}

      {board && board.entries.length === 0 ? (
        <p>No wins recorded yet today.</p>
      ) : (
        board && (
          <table className="leaderboard-table">
            <thead>
              <tr>
                <th>#</th>
                <th>Player</th>
                <th>Wins</th>
              </tr>
            </thead>
            <tbody>
              {board.entries.map((entry) => (
                <tr key={entry.userId}>
                  <td>{entry.rank}</td>
                  <td>{entry.username}</td>
                  <td>{entry.wins}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )
      )}

      <p className="links">
        <button type="button" onClick={() => void load()} disabled={loading}>
          Refresh
        </button>
      </p>
    </div>
  );
}