import { HubConnectionState } from "../lib/protocol";

interface GameHeaderProps {
  title: string;
  state: HubConnectionState;
  onBack: () => void;
  onLogout: () => void;
}

export default function GameHeader({
  title,
  state,
  onBack,
  onLogout,
}: GameHeaderProps) {
  return (
    <header className="game-header">
      <h1>{title}</h1>
      <span>
        Connection: <strong>{state}</strong>
      </span>
      <button type="button" onClick={onBack}>
        Back to Lobby
      </button>
      <button type="button" onClick={onLogout}>
        Log Out
      </button>
    </header>
  );
}
