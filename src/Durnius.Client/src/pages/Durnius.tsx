import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  CARD_VALUES,
  SUITS,
  useGameSession,
} from "../lib/protocol";
import type {
  CardDto,
  CardValue,
  DurniusMove,
  DurniusRoundResultDto,
  DurniusStandingDto,
  DurniusStateDto,
  LobbyStateDto,
  Suit,
} from "../lib/protocol";
import { useAuth } from "../auth/AuthContext";
import GameHeader from "../components/GameHeader";
import PlayingCard from "../components/PlayingCard";

const MAX_TABLE_CARDS = 6;

function cardKey(card: CardDto): string {
  return `${card.suit}-${card.value}`;
}

function sameCard(left: CardDto | null, right: CardDto | null): boolean {
  return left !== null && right !== null && cardKey(left) === cardKey(right);
}

function sortHand(hand: CardDto[]): CardDto[] {
  return [...hand].sort(
    (left, right) =>
      SUITS.indexOf(left.suit) - SUITS.indexOf(right.suit) ||
      CARD_VALUES.indexOf(left.value) - CARD_VALUES.indexOf(right.value)
  );
}

function rank(value: CardValue): number {
  return CARD_VALUES.indexOf(value);
}

/** Same rule as GameEngine.CanBeat: higher of the same suit, or trump over a non-trump. */
function canBeat(attack: CardDto, defense: CardDto, trump: Suit): boolean {
  if (attack.suit === defense.suit) {
    return rank(defense.value) > rank(attack.value);
  }
  return defense.suit === trump && attack.suit !== trump;
}

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
  const { logout } = useAuth();
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
  const session = useGameSession<DurniusStateDto, DurniusRoundResultDto, DurniusMove>(
    "durnius",
    {
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
    }
  );

  async function send(move: DurniusMove) {
    setBusy(true);
    await session.move(move);
    setBusy(false);
  }

  function clearSelection() {
    setSelectedCard(null);
    setSelectedAttack(null);
  }

  function handleLogout() {
    logout();
    navigate("/login", { replace: true });
  }

  const table = game?.table ?? [];
  const isAttacker = game !== null && game.currentAttacker === game.you;
  const isDefender = game !== null && game.currentDefender === game.you;
  const waitingForAttack = isAttacker && table.length === 0;
  const selectedAttackEntry = selectedAttack
    ? table.find(
        (played) =>
          played.defenseCard === null && sameCard(played.attackCard, selectedAttack)
      )
    : undefined;

  const canAttack =
    isAttacker &&
    selectedCard !== null &&
    table.length < MAX_TABLE_CARDS &&
    canAddAttack(selectedCard, table);
  const canDefend =
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
      <GameHeader
        title="Durnius"
        state={session.state}
        onBack={() => navigate("/lobby")}
        onLogout={handleLogout}
      />
      {session.error && <p className="error">{session.error}</p>}
      {(session.actionError || notice) && (
        <p className="error">{session.actionError ?? notice}</p>
      )}

      {game === null ? (
        <div>
          <h2>Matchmaking</h2>
          {lobby ? (
            <>
              <p>
                Waiting for players ({lobby.count}/{lobby.capacity}):{" "}
                {lobby.players.join(", ")}
              </p>
              <button
                type="button"
                onClick={() => void session.leave()}
                disabled={!session.connected}
              >
                Leave Lobby
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => void session.join()}
              disabled={!session.connected}
            >
              Join Durnius Lobby
            </button>
          )}
        </div>
      ) : (
        <div className="durnius-game">
          {winner !== null && (
            <div className="round-banner game-over">
              <strong>{game.players[winner] ?? "A player"} wins the match!</strong>
              {standings.length > 0 && (
                <ol>
                  {standings.map((standing) => (
                    <li key={standing.playerIndex}>
                      {standing.username} — #{standing.placement}
                    </li>
                  ))}
                </ol>
              )}
              <button type="button" onClick={() => navigate("/lobby")}>
                Back to Lobby
              </button>
            </div>
          )}

          <div className="durnius-info">
            <span className="durnius-trump">
              Trump: <PlayingCard card={game.trumpCard} />
            </span>
            <span>Cards in deck: {game.deckCount}</span>
            <span>
              Turn: <strong>{game.players[game.currentAttacker]}</strong> →{" "}
              <strong>{game.players[game.currentDefender]}</strong>
            </span>
          </div>

          <div className="durnius-players">
            {game.players.map((name, index) => (
              <div
                key={index}
                className={`durnius-player${
                  index === game.you ? " durnius-player-you" : ""
                }`}
              >
                <strong>
                  {name}
                  {index === game.you ? " (you)" : ""}
                </strong>
                <span>{game.cardCounts[index] ?? 0} cards</span>
                {index === game.currentAttacker && <span className="badge">attacker</span>}
                {index === game.currentDefender && <span className="badge">defender</span>}
                {game.finishedPlayers.includes(index) && (
                  <span className="badge">#{game.playerPlacements[index]}</span>
                )}
              </div>
            ))}
          </div>

          {round && (
            <p className="round-banner">
              {round.defenderTookCards
                ? `${game.players[round.defender]} took ${round.cardsOnTable.length} ${
                    round.cardsOnTable.length === 1 ? "card" : "cards"
                  }.`
                : `${game.players[round.defender]} beat the attack.`}{" "}
              Next attacker: {game.players[round.nextAttacker]}.
            </p>
          )}

          <h2>Table</h2>
          <div className="durnius-table">
            {table.length === 0 ? (
              <p className="table-hint">No cards on the table.</p>
            ) : (
              table.map((played, index) => (
                <div
                  className="durnius-pair"
                  key={`${cardKey(played.attackCard)}-${index}`}
                >
                  <span className="durnius-pair-owner">
                    {game.players[played.playerIndex] ?? "Player"}
                  </span>
                  <button
                    type="button"
                    className={`card-button${
                      played.defenseCard === null &&
                      sameCard(played.attackCard, selectedAttack)
                        ? " selected"
                        : ""
                    }`}
                    disabled={!isDefender || played.defenseCard !== null || actionsDisabled}
                    onClick={() => setSelectedAttack(played.attackCard)}
                    aria-label={`Target ${played.attackCard.value} of ${played.attackCard.suit}`}
                  >
                    <PlayingCard card={played.attackCard} />
                  </button>
                  {played.defenseCard ? (
                    <>
                      <span className="durnius-arrow">←</span>
                      <PlayingCard card={played.defenseCard} />
                    </>
                  ) : (
                    <span className="durnius-undefended">undefended</span>
                  )}
                </div>
              ))
            )}
          </div>

          <h2>Your hand</h2>
          <div className="durnius-hand">
            {sortHand(game.hand).map((card) => (
              <button
                type="button"
                key={cardKey(card)}
                className={`card-button${
                  sameCard(card, selectedCard) ? " selected" : ""
                }`}
                aria-pressed={sameCard(card, selectedCard)}
                disabled={actionsDisabled}
                onClick={() => setSelectedCard(card)}
              >
                <PlayingCard card={card} />
              </button>
            ))}
            {game.hand.length === 0 && <p className="table-hint">Your hand is empty.</p>}
          </div>

          <div className="durnius-actions">
            {isAttacker && (
              <button
                type="button"
                disabled={!canAttack || actionsDisabled}
                onClick={() => {
                  if (selectedCard) void send({ type: "attack", card: selectedCard });
                }}
              >
                {waitingForAttack ? "Attack" : "Add attack"}
              </button>
            )}
            {isAttacker && table.length > 0 && (
              <button
                type="button"
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
                disabled={!canTransfer || actionsDisabled}
                onClick={() => {
                  if (selectedCard) void send({ type: "transfer", card: selectedCard });
                }}
              >
                Transfer
              </button>
            )}
            {isDefender && (
              <button
                type="button"
                disabled={table.length === 0 || actionsDisabled}
                onClick={() => void send({ type: "take" })}
              >
                Take cards
              </button>
            )}
            <button
              type="button"
              onClick={clearSelection}
              disabled={selectedCard === null && selectedAttack === null}
            >
              Clear selection
            </button>
          </div>

          <p>{status}</p>
          <p className="durnius-hint">
            Beat a card with a higher card of the same suit, or any trump against a
            non-trump. Added attacks must match a rank already on the table; you can
            transfer an attack before any defense is played.
          </p>
        </div>
      )}
    </>
  );
}
