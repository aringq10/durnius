import type { GameName, LobbyStateDto } from "../lib/protocol";

interface WaitingRoomProps {
  game: GameName;
  blurb: string;
  connected: boolean;
  lobby: LobbyStateDto | null;
  error: string | null;
  onJoin: () => void;
  onLeave: () => void;
}

/** Shared matchmaking panel: join the queue, watch the seats fill up. */
export default function WaitingRoom({
  game,
  blurb,
  connected,
  lobby,
  error,
  onJoin,
  onLeave,
}: WaitingRoomProps) {
  const title = game === "karas" ? "Karas" : "Durnius";

  return (
    <div className="panel waiting">
      {lobby ? (
        <>
          <h2>Waiting for players</h2>
          <div className="seats">
            {Array.from({ length: lobby.capacity }, (_, index) => {
              const name = lobby.players[index];
              return name ? (
                <span key={index} className="seat seat-filled">
                  {name}
                </span>
              ) : (
                <span key={index} className="seat seat-empty">
                  <span className="pulse-dot" aria-hidden="true" />
                  Waiting…
                </span>
              );
            })}
          </div>
          <p className="muted">
            {lobby.count}/{lobby.capacity} players — the match starts as soon
            as the table is full.
          </p>
          <button
            type="button"
            className="btn"
            onClick={onLeave}
            disabled={!connected}
          >
            Leave queue
          </button>
        </>
      ) : (
        <>
          <h2>Join the {title} queue</h2>
          <p className="muted">{blurb}</p>
          <button
            type="button"
            className="btn btn-primary"
            onClick={onJoin}
            disabled={!connected}
          >
            {connected ? "Join queue" : "Connecting…"}
          </button>
        </>
      )}
      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}
    </div>
  );
}
