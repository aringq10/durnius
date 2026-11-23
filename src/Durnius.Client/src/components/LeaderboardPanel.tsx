import { useCallback, useEffect, useState } from "react";
import { errorMessage, getLeaderboard } from "../lib/api";
import type { LeaderboardResponse } from "../lib/api";

/** Wait this long before retrying a failed request. */
const RETRY_MS = 30_000;

/** Daily leaderboard panel shown on the lobby page. */
export default function LeaderboardPanel() {
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

  // Initial load, plus a retry after failures (retryTick bumps the effect).
  useEffect(() => {
    void load();
  }, [load, retryTick]);

  useEffect(() => {
    if (!error) return;
    const timer = window.setTimeout(
      () => setRetryTick((tick) => tick + 1),
      RETRY_MS
    );
    return () => window.clearTimeout(timer);
  }, [error]);

  // Refresh exactly when the UTC day rolls over, using the server's boundary.
  useEffect(() => {
    if (!board || loading) return;
    const delay =
      Math.max(new Date(board.resetAtUtc).getTime() - Date.now(), 0) + 1000;
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
    <section className="panel leaderboard">
      <div className="panel-head">
        <div>
          <h2>Daily leaderboard</h2>
          <p className="muted">
            Most wins today (UTC)
            {resetTime ? ` · resets at ${resetTime} UTC` : ""}
          </p>
        </div>
        <button
          type="button"
          className="btn btn-sm"
          onClick={() => void load()}
          disabled={loading}
        >
          {loading ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {loading && !board && <p className="muted">Loading…</p>}
      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}

      {board && board.entries.length === 0 && (
        <p className="muted">No wins recorded yet today. Be the first.</p>
      )}

      {board && board.entries.length > 0 && (
        <table className="board-table">
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
      )}
    </section>
  );
}
