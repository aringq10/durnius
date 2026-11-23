import { useCallback, useEffect, useState } from "react";
import { errorMessage, getMatchHistory } from "../lib/api";
import type { MatchHistoryEntry } from "../lib/api";

/** "karas" → "Karas"; unknown game types are shown capitalized as-is. */
function gameLabel(gameType: string): string {
  return gameType.charAt(0).toUpperCase() + gameType.slice(1);
}

function formatDate(matchDateUtc: string): string {
  return new Date(matchDateUtc).toLocaleString([], {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

/** Last 10 games played by the signed-in player, shown on the account page. */
export default function MatchHistoryPanel() {
  const [entries, setEntries] = useState<MatchHistoryEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getMatchHistory();
      setEntries(data);
      setError(null);
    } catch (err: unknown) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <section className="panel">
      <div className="panel-head">
        <div>
          <h2>Match history</h2>
          <p className="muted">Your last 10 games.</p>
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

      {loading && !entries && <p className="muted">Loading…</p>}
      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}

      {entries && entries.length === 0 && (
        <p className="muted">No games played yet. Join a lobby to get started.</p>
      )}

      {entries && entries.length > 0 && (
        <table className="match-table">
          <thead>
            <tr>
              <th>Placement</th>
              <th>Game</th>
              <th className="match-date">Date played</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, index) => (
              <tr key={`${entry.matchDateUtc}-${index}`}>
                <td
                  className={
                    entry.placement === 1 ? "match-win" : undefined
                  }
                >
                  {entry.placement === null ? "—" : `#${entry.placement}`}
                </td>
                <td>{gameLabel(entry.gameType)}</td>
                <td className="match-date">{formatDate(entry.matchDateUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
