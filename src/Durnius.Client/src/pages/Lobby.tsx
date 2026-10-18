import { useEffect, useRef, useState } from "react";
import type { CSSProperties, KeyboardEvent } from "react";
import { useNavigate } from "react-router-dom";
import { HubConnectionState } from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";
import { errorMessage } from "../lib/api";
import { createLobbyConnection } from "../lib/hub";
import type {
  DrawStatusDto,
  LobbyStateDto,
  MatchStartedDto,
  RoundResultDto,
} from "../lib/hub";
import { useAuth } from "../auth/AuthContext";
import PlayingCard from "../components/PlayingCard";

/** How long the revealed top cards stay before a war starts, and the delay between war pairs. */
const REVEAL_MS = 600;
const WAR_STEP_MS = 800;
/** How long the pile takes to fold into a deck, and to travel into the winner's deck. */
const FOLD_MS = 350;
const COLLECT_MS = 450;

export default function Lobby() {
  const { token, logout } = useAuth();
  const navigate = useNavigate();

  const connectionRef = useRef<HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<HubConnectionState>(
    HubConnectionState.Disconnected
  );
  const [connectError, setConnectError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [lobby, setLobby] = useState<LobbyStateDto | null>(null);

  const [match, setMatch] = useState<MatchStartedDto | null>(null);
  const [counts, setCounts] = useState({ player1: 0, player2: 0 });
  const [drawStatus, setDrawStatus] = useState<DrawStatusDto | null>(null);
  const [round, setRound] = useState<RoundResultDto | null>(null);
  const [roundNo, setRoundNo] = useState(0);
  const [revealedWar, setRevealedWar] = useState(0);
  const [animating, setAnimating] = useState(false);
  const [winner, setWinner] = useState<number | null>(null);
  const [drawing, setDrawing] = useState(false);
  /** 0 while cards lie flat, 1 while they fold together, 2 while the pile travels to a deck. */
  const [collecting, setCollecting] = useState<0 | 1 | 2>(0);
  const [collectShift, setCollectShift] = useState<{ x: number; y: number } | null>(
    null
  );

  const deck1Ref = useRef<HTMLDivElement>(null);
  const deck2Ref = useRef<HTMLDivElement>(null);
  const stackRef = useRef<HTMLDivElement>(null);

  // Open the SignalR connection and track the lobby and match.
  useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;
    const connection = createLobbyConnection(token);

    connection.on("JoinRejected", (reason: string) => {
      if (!cancelled) setActionError(reason);
    });

    connection.on("LobbyUpdate", (state: LobbyStateDto) => {
      if (cancelled) return;
      setLobby(state);
      setActionError(null);
    });

    connection.on("MatchStarted", (state: MatchStartedDto) => {
      if (cancelled) return;
      setMatch(state);
      setCounts({ player1: state.player1Count, player2: state.player2Count });
      setDrawStatus(null);
      setRound(null);
      setRevealedWar(0);
      setAnimating(false);
      setCollecting(0);
      setCollectShift(null);
      setWinner(null);
      setActionError(null);
    });

    connection.on("DrawStatus", (state: DrawStatusDto) => {
      if (cancelled) return;
      setDrawStatus(state);
      // Only one player has drawn, so a new round just started.
      if (!(state.player1Drawn && state.player2Drawn)) {
        setRound(null);
        setRevealedWar(0);
      }
    });

    connection.on("RoundResult", (result: RoundResultDto) => {
      if (cancelled) return;
      setRound(result);
      setRoundNo((value) => value + 1);
      setRevealedWar(0);
      setDrawStatus(null);
      setCollecting(0);
      setCollectShift(null);
      setAnimating(true);
    });

    connection.on("MatchEnded", (winner: number) => {
      if (!cancelled) setWinner(winner);
    });

    connection.onreconnecting(() => {
      if (!cancelled) setConnectionState(HubConnectionState.Reconnecting);
    });

    connection.onreconnected(() => {
      if (cancelled) return;
      // The server drops disconnected players from the lobby, so start over.
      setConnectionState(HubConnectionState.Connected);
      backToLobby();
    });

    connection.onclose(() => {
      if (!cancelled) setConnectionState(HubConnectionState.Disconnected);
    });

    connection
      .start()
      .then(() => {
        if (cancelled) {
          void connection.stop();
          return;
        }
        setConnectionState(HubConnectionState.Connected);
      })
      .catch((err: unknown) => {
        if (!cancelled) setConnectError(errorMessage(err));
      });

    connectionRef.current = connection;
    setConnectionState(connection.state);
    setConnectError(null);

    return () => {
      cancelled = true;
      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
      void connection.stop();
    };
    // backToLobby is stable enough for this effect; it only resets local state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  // Reveal war pairs one at a time, fold the pile into a deck, slide it into
  // the round winner's side, then unlock drawing and settle the deck counts.
  useEffect(() => {
    if (!round) return;

    const timers: number[] = [];
    const revealDoneMs = WAR_STEP_MS * round.war.length + REVEAL_MS;

    round.war.forEach((_, index) => {
      timers.push(
        window.setTimeout(() => setRevealedWar(index + 1), WAR_STEP_MS * (index + 1))
      );
    });

    timers.push(
      window.setTimeout(() => {
        setCollecting(1);
      }, revealDoneMs)
    );

    timers.push(
      window.setTimeout(() => {
        // Measure now, while the pile is still centered, so it lands on the deck.
        const stack = stackRef.current;
        const deck = (round.roundWinner === 1 ? deck1Ref : deck2Ref).current;
        // Aim at the deck's card rather than the whole deck column, so long
        // player names above/below the card can't skew the landing spot.
        const target = deck?.querySelector<HTMLElement>(".playing-card") ?? deck;
        if (stack && target) {
          const from = stack.getBoundingClientRect();
          const to = target.getBoundingClientRect();
          setCollectShift({
            x: to.left + to.width / 2 - (from.left + from.width / 2),
            y: to.top + to.height / 2 - (from.top + from.height / 2),
          });
        }
        setCollecting(2);
      }, revealDoneMs + FOLD_MS)
    );

    timers.push(
      window.setTimeout(() => {
        setCollecting(0);
        setCollectShift(null);
        setRound(null);
        setAnimating(false);
        setCounts({ player1: round.player1Count, player2: round.player2Count });
      }, revealDoneMs + FOLD_MS + COLLECT_MS)
    );

    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [round]);

  function backToLobby() {
    setLobby(null);
    setMatch(null);
    setCounts({ player1: 0, player2: 0 });
    setDrawStatus(null);
    setRound(null);
    setRevealedWar(0);
    setAnimating(false);
    setCollecting(0);
    setCollectShift(null);
    setWinner(null);
    setActionError(null);
  }

  function handleLogout() {
    // JWT auth is stateless: logging out only means discarding the token.
    logout();
    navigate("/login", { replace: true });
  }

  async function invoke(method: "JoinLobby" | "LeaveLobby" | "Draw") {
    const connection = connectionRef.current;
    if (!connection) return;

    setActionError(null);
    try {
      await connection.invoke(method);
    } catch (err: unknown) {
      setActionError(errorMessage(err));
    }
  }

  async function handleDraw() {
    setDrawing(true);
    await invoke("Draw");
    setDrawing(false);
  }

  function handleDeckKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      void handleDraw();
    }
  }

  const connected = connectionState === HubConnectionState.Connected;

  if (match) {
    const game = match;
    const you = game.you;
    const alreadyDrawn = drawStatus
      ? you === 1
        ? drawStatus.player1Drawn
        : drawStatus.player2Drawn
      : false;
    const opponentDrawn = drawStatus
      ? you === 1
        ? drawStatus.player2Drawn
        : drawStatus.player1Drawn
      : false;
    const canDraw =
      connected && winner === null && !animating && !drawing && !alreadyDrawn;

    // Cards leave the decks while the round is being revealed.
    const inPlay = animating && round ? 1 + revealedWar : 0;
    const shownCounts = {
      player1: Math.max(0, counts.player1 - inPlay),
      player2: Math.max(0, counts.player2 - inPlay),
    };

    let status: string;
    if (winner !== null) {
      status = `${winner === 1 ? match.player1 : match.player2} wins the match!`;
    } else if (round && collecting > 0) {
      status = `${
        round.roundWinner === 1 ? match.player1 : match.player2
      } takes the pile.`;
    } else if (animating) {
      status = "Resolving round…";
    } else if (opponentDrawn) {
      status = "Opponent drew. Click your deck to draw.";
    } else if (alreadyDrawn) {
      status = "You drew. Waiting for opponent…";
    } else {
      status = "Click your deck to draw.";
    }

    // Your own deck is the draw control; the opponent's is just a pile of cards.
    function renderDeck(player: 1 | 2) {
      const name = player === 1 ? game.player1 : game.player2;
      const own = you === player;
      const drawReady = own && canDraw;
      return (
        <div
          className={`deck${drawReady ? " deck-draw" : ""}`}
          ref={player === 1 ? deck1Ref : deck2Ref}
          role={drawReady ? "button" : undefined}
          tabIndex={drawReady ? 0 : undefined}
          aria-label={drawReady ? "Draw a card" : undefined}
          onClick={drawReady ? () => void handleDraw() : undefined}
          onKeyDown={drawReady ? handleDeckKey : undefined}
        >
          <span className="deck-name">
            {name}
            {own ? " (you)" : ""}
          </span>
          <PlayingCard faceDown />
          <span className="deck-count">
            {player === 1 ? shownCounts.player1 : shownCounts.player2} cards
          </span>
        </div>
      );
    }

    return (
      <>
        <p>
          Connection: <strong>{connectionState}</strong>
        </p>
        {actionError && <p className="error">{actionError}</p>}

        <div className="board">
          {renderDeck(1)}

          <div className="table">
            {round ? (
              <div
                className={`table-cards${
                  collecting > 0 ? " table-fold" : ""
                }${collecting === 2 ? " table-collect" : ""}`}
                ref={stackRef}
                style={
                  collecting === 2
                    ? ({
                        "--collect-x": `${collectShift?.x ?? 0}px`,
                        "--collect-y": `${collectShift?.y ?? 0}px`,
                        "--collect-rot": `${round.roundWinner === 1 ? -8 : 8}deg`,
                      } as CSSProperties)
                    : undefined
                }
              >
                <div className="table-row card-in" key={`round-${roundNo}`}>
                  <PlayingCard card={round.player1Card} />
                  <PlayingCard card={round.player2Card} />
                </div>
                {round.war.slice(0, revealedWar).map((pair, index) => (
                  <div
                    className="table-row table-row-war card-in"
                    key={`war-${roundNo}-${index}`}
                  >
                    <PlayingCard card={pair.player1} />
                    <PlayingCard card={pair.player2} />
                  </div>
                ))}
              </div>
            ) : (
              <p className="table-hint">No cards on the table.</p>
            )}
          </div>

          {renderDeck(2)}
        </div>

        <p>{status}</p>
        <p>
          {winner !== null && (
            <button type="button" onClick={backToLobby}>
              Back to Lobby
            </button>
          )}
        </p>

        <p>
          <button type="button" onClick={handleLogout}>
            Log Out
          </button>
        </p>
      </>
    );
  }

  return (
    <>
      <h1>Lobby</h1>
      <p>
        Connection: <strong>{connectionState}</strong>
      </p>
      {connectError && <p className="error">{connectError}</p>}
      {actionError && <p className="error">{actionError}</p>}

      {lobby ? (
        <div>
          <p>
            Waiting for players ({lobby.count}/{lobby.capacity}):{" "}
            {lobby.players.join(", ")}
          </p>
          <button
            type="button"
            onClick={() => void invoke("LeaveLobby")}
            disabled={!connected}
          >
            Leave Lobby
          </button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => void invoke("JoinLobby")}
          disabled={!connected}
        >
          Join Lobby
        </button>
      )}

      <p>
        <button type="button" onClick={handleLogout}>
          Log Out
        </button>
      </p>
    </>
  );
}
