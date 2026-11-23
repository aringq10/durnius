import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useGameSession } from "../lib/protocol";
import type {
  DurniusMove,
  DurniusRoundResultDto,
  DurniusStandingDto,
  DurniusStateDto,
  LobbyStateDto,
} from "../lib/protocol";
import {
  canBeat,
  cardKey,
  sameCard,
  sortHand,
} from "../lib/cards";
import type { CardDto } from "../lib/cards";
import ConnectionDot from "../components/ConnectionDot";
import PlayingCard from "../components/PlayingCard";
import WaitingRoom from "../components/WaitingRoom";

const MAX_TABLE_CARDS = 6;

/** Added attacks must match a rank already on the table (attack or defense). */
function canAddAttack(card: CardDto, table: DurniusStateDto["table"]): boolean {
  return (
    table.length === 0 ||
    table.some((played) =>
      [played.attackCard, played.defenseCard].some(
        (onTable) => onTable?.value === card.value
      )
    )
  );
}

export default function Durnius() {
  const navigate = useNavigate();

  const [lobby, setLobby] = useState<LobbyStateDto | null>(null);
  const [game, setGame] = useState<DurniusStateDto | null>(null);
  const [round, setRound] = useState<DurniusRoundResultDto | null>(null);
  const [standings, setStandings] = useState<DurniusStandingDto[]>([]);
  const [winner, setWinner] = useState<number | null>(null);
  const [selectedCard, setSelectedCard] = useState<CardDto | null>(null);
  const [selectedAttack, setSelectedAttack] = useState<CardDto | null>(null);
  const [passed, setPassed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  function reset() {
    setLobby(null);
    setGame(null);
    setRound(null);
    setStandings([]);
    setWinner(null);
    setSelectedCard(null);
    setSelectedAttack(null);
    setPassed(false);
    setBusy(false);
    setNotice(null);
  }

  function applyState(next: DurniusStateDto) {
    setGame(next);
    setRound(next.lastRound);
    setSelectedCard(null);
    setSelectedAttack(null);
    setPassed(false);
    setNotice(null);
  }

  // Every hub message and invocation lives in the protocol module.
  const session = useGameSession<
    DurniusStateDto,
    DurniusRoundResultDto,
    DurniusMove
  >("durnius", {
    onJoinRejected: (reason) => setNotice(reason),
    onLobby: (state) => {
      setLobby(state);
      setNotice(null);
    },
    onMatchStarted: applyState,
    onGameState: applyState,
    onRoundResult: setRound,
    onMatchEnded: (winnerIndex) => setWinner(winnerIndex),
    onMatchStandings: setStandings,
    onReconnect: reset,
  });

  async function send(move: DurniusMove) {
    setBusy(true);
    await session.move(move);
    setBusy(false);
  }

  function handleLeave() {
    setLobby(null);
    void session.leave();
  }

  function handlePlayAgain() {
    reset();
    void session.join();
  }

  const table = game?.table ?? [];
  const isAttacker = game !== null && game.currentAttacker === game.you;
  const isDefender = game !== null && game.currentDefender === game.you;
  const waitingForAttack = isAttacker && table.length === 0;
  const selectedAttackEntry = selectedAttack
    ? table.find(
        (played) =>
          played.defenseCard === null &&
          sameCard(played.attackCard, selectedAttack)
      )
    : undefined;

  const canAttack =
    isAttacker &&
    selectedCard !== null &&
    table.length < MAX_TABLE_CARDS &&
    canAddAttack(selectedCard, table);
  const canDefend =
    game !== null &&
    isDefender &&
    selectedCard !== null &&
    selectedAttackEntry !== undefined &&
    canBeat(selectedAttackEntry.attackCard, selectedCard, game.trumpSuit);
  const canTransfer =
    isDefender &&
    selectedCard !== null &&
    table.length > 0 &&
    table.every((played) => played.defenseCard === null) &&
    table.some((played) => played.attackCard.value === selectedCard.value);
  const canPass = isAttacker && table.length > 0 && !passed;
  const actionsDisabled = busy || winner !== null;

  let status: string;
  if (game === null) {
    status = "";
  } else if (winner !== null) {
    status = `${game.players[winner] ?? "A player"} wins the match!`;
  } else if (waitingForAttack) {
    status = "You open the round. Pick a card from your hand to attack.";
  } else if (isAttacker) {
    status =
      "Add another card of a matching rank, or pass if you are done attacking.";
  } else if (isDefender) {
    status =
      "Pick an undefended attack card, then beat it with your hand — or transfer or take.";
  } else {
    status = `${game.players[game.currentAttacker]} is attacking ${game.players[game.currentDefender]}.`;
  }

  return (
    <>
      <div className="game-head">
        <h1>Durnius</h1>
        <ConnectionDot state={session.state} />
      </div>

      {session.error && (
        <div className="alert alert-error" role="alert">
          {session.error}{" "}
          <button type="button" className="btn btn-sm" onClick={session.retry}>
            Retry
          </button>
        </div>
      )}

      {game === null ? (
        <WaitingRoom
          game="durnius"
          blurb="Attack and defend, durak-style. Beat the cards thrown at you with a higher card of the same suit or a trump — or take them all. The last player holding cards loses."
          connected={session.connected}
          lobby={lobby}
          error={notice ?? session.actionError}
          onJoin={() => void session.join()}
          onLeave={handleLeave}
        />
      ) : (
        <div className="durnius">
          {winner !== null && (
            <div className="panel gameover">
              <h2>
                {winner === game.you ? "You win the match!" : status}
              </h2>
              {standings.length > 0 && (
                <ol className="standings">
                  {standings.map((standing) => (
                    <li key={standing.playerIndex}>
                      <span className="standings-place">
                        #{standing.placement}
                      </span>
                      {standing.username}
                      {standing.playerIndex === game.you ? " (you)" : ""}
                    </li>
                  ))}
                </ol>
              )}
              <div className="gameover-actions">
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={handlePlayAgain}
                  disabled={!session.connected}
                >
                  Play again
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => navigate("/lobby")}
                >
                  Back to lobby
                </button>
              </div>
            </div>
          )}

          <div className="durnius-info">
            <span className="durnius-trump">
              Trump <PlayingCard card={game.trumpCard} size="sm" />
            </span>
            <span className="durnius-deck">
              <PlayingCard faceDown size="sm" />
              {game.deckCount} in deck
            </span>
            <span className="durnius-turn">
              <strong>{game.players[game.currentAttacker]}</strong>
              <span className="turn-arrow" aria-hidden="true">
                →
              </span>
              <strong>{game.players[game.currentDefender]}</strong>
            </span>
          </div>

          <div className="players-strip">
            {game.players.map((name, index) => (
              <div
                key={index}
                className={`player-chip${
                  index === game.you ? " player-chip-you" : ""
                }`}
              >
                <strong>
                  {name}
                  {index === game.you ? " (you)" : ""}
                </strong>
                <span className="muted">
                  {game.cardCounts[index] ?? 0} cards
                </span>
                {index === game.currentAttacker && (
                  <span className="badge badge-attack">attacker</span>
                )}
                {index === game.currentDefender && (
                  <span className="badge badge-defend">defender</span>
                )}
                {game.finishedPlayers.includes(index) && (
                  <span className="badge">
                    #{game.playerPlacements[index]}
                  </span>
                )}
              </div>
            ))}
          </div>

          {round && winner === null && (
            <div className="alert alert-info" role="status">
              {round.defenderTookCards
                ? `${game.players[round.defender]} took ${
                    round.cardsOnTable.length
                  } ${round.cardsOnTable.length === 1 ? "card" : "cards"}.`
                : `${game.players[round.defender]} beat the attack.`}{" "}
              Next attacker: {game.players[round.nextAttacker]}.
            </div>
          )}

          <div className="felt durnius-table">
            {table.length === 0 ? (
              <p className="table-hint">No cards on the table.</p>
            ) : (
              table.map((played, index) => (
                <div
                  className="pair"
                  key={`${cardKey(played.attackCard)}-${index}`}
                >
                  <button
                    type="button"
                    className={`card-btn pair-attack${
                      played.defenseCard === null &&
                      sameCard(played.attackCard, selectedAttack)
                        ? " selected"
                        : ""
                    }`}
                    disabled={
                      !isDefender ||
                      played.defenseCard !== null ||
                      actionsDisabled
                    }
                    onClick={() => setSelectedAttack(played.attackCard)}
                    aria-label={`Target ${played.attackCard.value} of ${played.attackCard.suit}`}
                  >
                    <PlayingCard card={played.attackCard} />
                  </button>
                  {played.defenseCard ? (
                    <span className="pair-defense">
                      <PlayingCard card={played.defenseCard} />
                    </span>
                  ) : (
                    <span className="pair-slot" aria-hidden="true" />
                  )}
                  <span className="pair-owner">
                    {game.players[played.playerIndex] ?? "Player"}
                  </span>
                </div>
              ))
            )}
          </div>

          <div className="hand">
            {sortHand(game.hand, game.trumpSuit).map((card) => (
              <button
                type="button"
                key={cardKey(card)}
                className={`card-btn${
                  sameCard(card, selectedCard) ? " selected" : ""
                }`}
                aria-pressed={sameCard(card, selectedCard)}
                disabled={actionsDisabled}
                onClick={() => setSelectedCard(card)}
              >
                <PlayingCard card={card} />
              </button>
            ))}
            {game.hand.length === 0 && (
              <p className="table-hint">Your hand is empty.</p>
            )}
          </div>

          <div className="action-bar">
            {isAttacker && (
              <button
                type="button"
                className="btn btn-primary"
                disabled={!canAttack || actionsDisabled}
                onClick={() => {
                  if (selectedCard) {
                    void send({ type: "attack", card: selectedCard });
                  }
                }}
              >
                {waitingForAttack ? "Attack" : "Add attack"}
              </button>
            )}
            {isAttacker && table.length > 0 && (
              <button
                type="button"
                className="btn"
                disabled={!canPass || actionsDisabled}
                onClick={() => {
                  setPassed(true);
                  void send({ type: "pass" });
                }}
              >
                Pass
              </button>
            )}
            {isDefender && (
              <button
                type="button"
                className="btn btn-primary"
                disabled={!canDefend || actionsDisabled}
                onClick={() => {
                  if (selectedCard && selectedAttackEntry) {
                    void send({
                      type: "defend",
                      attackCard: selectedAttackEntry.attackCard,
                      defenseCard: selectedCard,
                    });
                  }
                }}
              >
                Defend
              </button>
            )}
            {isDefender && (
              <button
                type="button"
                className="btn"
                disabled={!canTransfer || actionsDisabled}
                onClick={() => {
                  if (selectedCard) {
                    void send({ type: "transfer", card: selectedCard });
                  }
                }}
              >
                Transfer
              </button>
            )}
            {isDefender && (
              <button
                type="button"
                className="btn"
                disabled={table.length === 0 || actionsDisabled}
                onClick={() => void send({ type: "take" })}
              >
                Take cards
              </button>
            )}
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => {
                setSelectedCard(null);
                setSelectedAttack(null);
              }}
              disabled={selectedCard === null && selectedAttack === null}
            >
              Clear selection
            </button>
          </div>

          <p className="status">{status}</p>
          <p className="hint">
            Beat a card with a higher card of the same suit, or any trump
            against a non-trump. Added attacks must match a rank already on
            the table; you can transfer an attack before any defense is
            played. Leaving the page forfeits the match.
          </p>
        </div>
      )}
    </>
  );
}
