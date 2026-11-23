import { useEffect, useRef, useState } from "react";
import type { CSSProperties, KeyboardEvent, RefObject } from "react";
import { useNavigate } from "react-router-dom";
import { useGameSession } from "../lib/protocol";
import type {
  KarasDrawStatusDto,
  KarasMatchStartDto,
  KarasRoundResultDto,
  LobbyStateDto,
} from "../lib/protocol";
import ConnectionDot from "../components/ConnectionDot";
import PlayingCard from "../components/PlayingCard";
import WaitingRoom from "../components/WaitingRoom";

/** How long the revealed top cards stay before a war starts, and the delay between war pairs. */
const REVEAL_MS = 600;
const WAR_STEP_MS = 800;
/** How long the pile takes to fold into a deck, and to travel into the winner's deck. */
const FOLD_MS = 350;
const COLLECT_MS = 450;

export default function Karas() {
  const navigate = useNavigate();

  const [joinError, setJoinError] = useState<string | null>(null);
  const [lobby, setLobby] = useState<LobbyStateDto | null>(null);

  const [match, setMatch] = useState<KarasMatchStartDto | null>(null);
  const [counts, setCounts] = useState({ player1: 0, player2: 0 });
  const [drawStatus, setDrawStatus] = useState<KarasDrawStatusDto | null>(null);
  const [round, setRound] = useState<KarasRoundResultDto | null>(null);
  const [roundNo, setRoundNo] = useState(0);
  const [revealedWar, setRevealedWar] = useState(0);
  const [animating, setAnimating] = useState(false);
  const [winner, setWinner] = useState<number | null>(null);
  const [drawing, setDrawing] = useState(false);
  /** 0 while cards lie flat, 1 while they fold together, 2 while the pile travels to a deck. */
  const [collecting, setCollecting] = useState<0 | 1 | 2>(0);
  const [collectShift, setCollectShift] = useState<{
    x: number;
    y: number;
  } | null>(null);

  const deck1Ref = useRef<HTMLDivElement>(null);
  const deck2Ref = useRef<HTMLDivElement>(null);
  const stackRef = useRef<HTMLDivElement>(null);

  function reset() {
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
    setJoinError(null);
  }

  // The protocol module owns the connection, lobby join and all hub messages.
  const session = useGameSession<KarasMatchStartDto, KarasRoundResultDto>(
    "karas",
    {
      onJoinRejected: (reason) => setJoinError(reason),
      onLobby: (state) => {
        setLobby(state);
        setJoinError(null);
      },
      onMatchStarted: (state) => {
        setMatch(state);
        setCounts({ player1: state.player1Count, player2: state.player2Count });
        setDrawStatus(null);
        setRound(null);
        setRevealedWar(0);
        setAnimating(false);
        setCollecting(0);
        setCollectShift(null);
        setWinner(null);
        setJoinError(null);
      },
      onDrawStatus: (state) => {
        setDrawStatus(state);
        // Only one player has drawn, so a new round just started — unless
        // the previous round's reveal is still animating. Interrupting it
        // would orphan its timers and leave the board stuck, so let it
        // finish; the fresh draw status is already stored for afterwards.
        if (!(state.player1Drawn && state.player2Drawn) && !animating) {
          setRound(null);
          setRevealedWar(0);
        }
      },
      onRoundResult: (result) => {
        setRound(result);
        setRoundNo((value) => value + 1);
        setRevealedWar(0);
        setDrawStatus(null);
        setCollecting(0);
        setCollectShift(null);
        setAnimating(true);
      },
      onMatchEnded: (winnerIndex) => setWinner(winnerIndex),
      onReconnect: reset,
    }
  );

  // Reveal war pairs one at a time, fold the pile into a deck, slide it into
  // the round winner's side, then unlock drawing and settle the deck counts.
  useEffect(() => {
    if (!round) return;

    const timers: number[] = [];
    const revealDoneMs = WAR_STEP_MS * round.war.length + REVEAL_MS;

    round.war.forEach((_, index) => {
      timers.push(
        window.setTimeout(
          () => setRevealedWar(index + 1),
          WAR_STEP_MS * (index + 1)
        )
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
        const target = deck?.querySelector<HTMLElement>(".pcard") ?? deck;
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
        setCounts({
          player1: round.player1Count,
          player2: round.player2Count,
        });
      }, revealDoneMs + FOLD_MS + COLLECT_MS)
    );

    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [round]);

  async function handleDraw() {
    setDrawing(true);
    await session.move(null);
    setDrawing(false);
  }

  function handleDeckKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      void handleDraw();
    }
  }

  function handleLeave() {
    setLobby(null);
    void session.leave();
  }

  function handlePlayAgain() {
    reset();
    void session.join();
  }

  const connected = session.connected;

  return (
    <>
      <div className="game-head">
        <h1>Karas</h1>
        <ConnectionDot state={session.state} />
      </div>

      {session.error && (
        <div className="alert alert-error" role="alert">
          {session.error}{" "}
          <button
            type="button"
            className="btn btn-sm"
            onClick={session.retry}
          >
            Retry
          </button>
        </div>
      )}

      {match === null ? (
        <WaitingRoom
          game="karas"
          blurb="Two players split the deck — red suits against black. Each round both draw a card; the higher rank takes both. Ties mean war."
          connected={connected}
          lobby={lobby}
          error={joinError ?? session.actionError}
          onJoin={() => void session.join()}
          onLeave={handleLeave}
        />
      ) : (
        <MatchBoard
          match={match}
          counts={counts}
          drawStatus={drawStatus}
          round={round}
          roundNo={roundNo}
          revealedWar={revealedWar}
          animating={animating}
          winner={winner}
          drawing={drawing}
          collecting={collecting}
          collectShift={collectShift}
          connected={connected}
          deck1Ref={deck1Ref}
          deck2Ref={deck2Ref}
          stackRef={stackRef}
          onDraw={() => void handleDraw()}
          onDeckKey={handleDeckKey}
          onPlayAgain={handlePlayAgain}
          onBack={() => navigate("/lobby")}
          actionError={session.actionError}
        />
      )}
    </>
  );
}

interface MatchBoardProps {
  match: KarasMatchStartDto;
  counts: { player1: number; player2: number };
  drawStatus: KarasDrawStatusDto | null;
  round: KarasRoundResultDto | null;
  roundNo: number;
  revealedWar: number;
  animating: boolean;
  winner: number | null;
  drawing: boolean;
  collecting: 0 | 1 | 2;
  collectShift: { x: number; y: number } | null;
  connected: boolean;
  deck1Ref: RefObject<HTMLDivElement | null>;
  deck2Ref: RefObject<HTMLDivElement | null>;
  stackRef: RefObject<HTMLDivElement | null>;
  onDraw: () => void;
  onDeckKey: (event: KeyboardEvent<HTMLDivElement>) => void;
  onPlayAgain: () => void;
  onBack: () => void;
  actionError: string | null;
}

function MatchBoard({
  match,
  counts,
  drawStatus,
  round,
  roundNo,
  revealedWar,
  animating,
  winner,
  drawing,
  collecting,
  collectShift,
  connected,
  deck1Ref,
  deck2Ref,
  stackRef,
  onDraw,
  onDeckKey,
  onPlayAgain,
  onBack,
  actionError,
}: MatchBoardProps) {
  const you = match.you;
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
    const name = player === 1 ? match.player1 : match.player2;
    const own = you === player;
    const drawReady = own && canDraw;
    return (
      <div
        className={`deck${drawReady ? " deck-draw" : ""}`}
        ref={player === 1 ? deck1Ref : deck2Ref}
        role={drawReady ? "button" : undefined}
        tabIndex={drawReady ? 0 : undefined}
        aria-label={drawReady ? "Draw a card" : undefined}
        onClick={drawReady ? onDraw : undefined}
        onKeyDown={drawReady ? onDeckKey : undefined}
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
      {actionError && (
        <div className="alert alert-error" role="alert">
          {actionError}
        </div>
      )}

      {winner !== null && (
        <div className="panel gameover">
          <h2>{winner === you ? "You win the match!" : status}</h2>
          <p className="muted">
            Final piles — {match.player1}: {counts.player1} · {match.player2}:{" "}
            {counts.player2}
          </p>
          <div className="gameover-actions">
            <button
              type="button"
              className="btn btn-primary"
              onClick={onPlayAgain}
              disabled={!connected}
            >
              Play again
            </button>
            <button type="button" className="btn" onClick={onBack}>
              Back to lobby
            </button>
          </div>
        </div>
      )}

      <div className="karas-board felt">
        {renderDeck(1)}

        <div className="karas-table">
          {round ? (
            <div
              className={`karas-stack${collecting > 0 ? " table-fold" : ""}${
                collecting === 2 ? " table-collect" : ""
              }`}
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

      <p className="status">{status}</p>
      <p className="hint">
        Higher rank wins the round; suits don't matter. On a tie, each player
        stakes a card face-down and draws again — that's war.
      </p>
    </>
  );
}
