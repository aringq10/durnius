import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";
import { API_URL, errorMessage } from "./api";
import { useAuth } from "../auth/AuthContext";

export { HubConnectionState };

/** The two lobbies registered in MultiplayerServiceExtensions. */
export const LOBBIES = ["karas", "durnius"] as const;
export type GameName = (typeof LOBBIES)[number];

/** Card suits and values as serialized by the engines (lowercased enums). */
export type Suit = "hearts" | "diamonds" | "clubs" | "spades";
export type CardValue =
  | "two"
  | "three"
  | "four"
  | "five"
  | "six"
  | "seven"
  | "eight"
  | "nine"
  | "ten"
  | "jack"
  | "queen"
  | "king"
  | "ace";

export const SUITS: readonly Suit[] = ["hearts", "diamonds", "clubs", "spades"];
export const CARD_VALUES: readonly CardValue[] = [
  "two",
  "three",
  "four",
  "five",
  "six",
  "seven",
  "eight",
  "nine",
  "ten",
  "jack",
  "queen",
  "king",
  "ace",
];

export interface CardDto {
  suit: Suit;
  value: CardValue;
}

export const CARD_LABELS: Record<CardValue, string> = {
  two: "2",
  three: "3",
  four: "4",
  five: "5",
  six: "6",
  seven: "7",
  eight: "8",
  nine: "9",
  ten: "10",
  jack: "J",
  queen: "Q",
  king: "K",
  ace: "A",
};

export const SUIT_SYMBOLS: Record<Suit, string> = {
  hearts: "♥",
  diamonds: "♦",
  clubs: "♣",
  spades: "♠",
};

/** LobbyUpdate payload (see LobbyUpdateDto in Durnius.Web). */
export interface LobbyStateDto {
  capacity: number;
  count: number;
  players: string[];
}

// --- Karas messages -------------------------------------------------------

/** MatchStarted payload; `you` is 1 or 2, matching player1/player2. */
export interface KarasMatchStartDto {
  you: 1 | 2;
  player1: string;
  player2: string;
  player1Count: number;
  player2Count: number;
}

export interface KarasDrawStatusDto {
  player1Drawn: boolean;
  player2Drawn: boolean;
}

export interface KarasWarPairDto {
  player1: CardDto;
  player2: CardDto;
}

export interface KarasRoundResultDto {
  roundWinner: number;
  player1Card: CardDto;
  player2Card: CardDto;
  war: KarasWarPairDto[];
  player1Count: number;
  player2Count: number;
  gameOver: boolean;
  gameWinner: number;
}

// --- Durnius messages -----------------------------------------------------

export interface DurniusPlayedCardDto {
  playerIndex: number;
  attackCard: CardDto;
  defenseCard: CardDto | null;
}

export interface DurniusRoundResultDto {
  attacker: number;
  defender: number;
  defenderTookCards: boolean;
  cardsOnTable: DurniusPlayedCardDto[];
  playerCardCounts: number[];
  nextAttacker: number;
  finishedPlayers: number[];
  playerPlacements: number[];
  gameOver: boolean;
  gameWinner: number | null;
  gameLoser: number | null;
}

export interface DurniusStandingDto {
  playerIndex: number;
  username: string;
  placement: number;
}

/** Private GameState/MatchStarted payload; only `hand` differs per player. */
export interface DurniusStateDto {
  you: number;
  players: string[];
  hand: CardDto[];
  cardCounts: number[];
  table: DurniusPlayedCardDto[];
  deckCount: number;
  trumpSuit: Suit;
  trumpCard: CardDto;
  currentAttacker: number;
  currentDefender: number;
  lastRound: DurniusRoundResultDto | null;
  finishedPlayers: number[];
  playerPlacements: number[];
  gameOver: boolean;
  gameWinner: number | null;
  gameLoser: number | null;
}

/** Move shapes accepted by DurniusMatch.MakeMove. */
export type DurniusMove =
  | { type: "attack"; card: CardDto }
  | { type: "defend"; attackCard: CardDto; defenseCard: CardDto }
  | { type: "transfer"; card: CardDto }
  | { type: "pass" }
  | { type: "take" };

// --- Transport ------------------------------------------------------------

/** Thin typed wrapper around the /hubs/lobby SignalR connection. */
export class GameClient {
  private readonly connection: HubConnection;

  constructor(token: string) {
    this.connection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/lobby`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
  }

  get state(): HubConnectionState {
    return this.connection.state;
  }

  on<T>(method: string, handler: (payload: T) => void): void {
    this.connection.on(method, handler);
  }

  onReconnecting(handler: () => void): void {
    this.connection.onreconnecting(handler);
  }

  onReconnected(handler: () => void): void {
    this.connection.onreconnected(handler);
  }

  onClose(handler: () => void): void {
    this.connection.onclose(handler);
  }

  start(): Promise<void> {
    return this.connection.start();
  }

  stop(): Promise<void> {
    return this.connection.stop();
  }

  join(game: GameName): Promise<void> {
    return this.connection.invoke("JoinLobby", game);
  }

  leave(): Promise<void> {
    return this.connection.invoke("LeaveLobby");
  }

  move(move: unknown): Promise<void> {
    return this.connection.invoke("MakeMove", move);
  }
}

// --- Session hook ---------------------------------------------------------

export interface GameSessionHandlers<TStart, TRound> {
  onJoinRejected?: (reason: string) => void;
  onLobby?: (state: LobbyStateDto) => void;
  onMatchStarted: (state: TStart) => void;
  onGameState?: (state: TStart) => void;
  onDrawStatus?: (status: KarasDrawStatusDto) => void;
  onRoundResult?: (result: TRound) => void;
  onMatchEnded?: (winner: number) => void;
  onMatchStandings?: (standings: DurniusStandingDto[]) => void;
  /** Called after a reconnect, before the hook re-joins the lobby. */
  onReconnect?: () => void;
}

export interface GameSession<TMove = unknown> {
  state: HubConnectionState;
  connected: boolean;
  error: string | null;
  actionError: string | null;
  clearActionError: () => void;
  join: () => Promise<void>;
  leave: () => Promise<void>;
  move: (move: TMove) => Promise<void>;
}

/** Rejections that can be resolved by re-joining once the old session is gone. */
const RETRYABLE_REJECTIONS = new Set([
  "Already in this lobby",
  "Already in another lobby",
  "Already in a match",
]);

async function joinAfterConnect(
  client: GameClient,
  game: GameName,
  isCancelled: () => boolean,
  onError: (message: string) => void
): Promise<void> {
  try {
    await client.join(game);
  } catch (err) {
    if (!isCancelled()) onError(errorMessage(err));
  }
}

/**
 * Owns the whole client-server session: connection lifetime, every hub event
 * and all hub invocations. Game pages only supply typed handlers for the
 * messages they care about.
 */
export function useGameSession<TStart, TRound, TMove = unknown>(
  game: GameName,
  handlers: GameSessionHandlers<TStart, TRound>
): GameSession<TMove> {
  const { token } = useAuth();
  const [state, setState] = useState<HubConnectionState>(
    HubConnectionState.Disconnected
  );
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const clientRef = useRef<GameClient | null>(null);
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  useEffect(() => {
    if (!token) return;

    let cancelled = false;
    let startTimer: number | undefined;
    let joinTimer: number | undefined;
    let retries = 0;
    const client = new GameClient(token);
    clientRef.current = client;
    setState(client.state);
    setError(null);
    setActionError(null);

    function scheduleJoin(delay = 0) {
      if (joinTimer !== undefined) window.clearTimeout(joinTimer);
      // Deferring also lets a StrictMode mount/unmount cycle cancel the join
      // before it reaches the server.
      joinTimer = window.setTimeout(() => {
        joinTimer = undefined;
        if (!cancelled) {
          void joinAfterConnect(client, game, () => cancelled, setActionError);
        }
      }, delay);
    }

    client.on<string>("JoinRejected", (reason) => {
      if (cancelled) return;
      // A previous connection with the same user may still be registered
      // during a fast reconnect or a dev StrictMode remount.
      if (RETRYABLE_REJECTIONS.has(reason) && retries < 3) {
        retries += 1;
        scheduleJoin(400);
        return;
      }
      handlersRef.current.onJoinRejected?.(reason);
    });
    client.on<LobbyStateDto>("LobbyUpdate", (lobby) => {
      if (!cancelled) handlersRef.current.onLobby?.(lobby);
    });
    client.on<TStart>("MatchStarted", (start) => {
      if (cancelled) return;
      setActionError(null);
      handlersRef.current.onMatchStarted(start);
    });
    client.on<TStart>("GameState", (next) => {
      if (cancelled) return;
      setActionError(null);
      handlersRef.current.onGameState?.(next);
    });
    client.on<KarasDrawStatusDto>("DrawStatus", (status) => {
      if (!cancelled) handlersRef.current.onDrawStatus?.(status);
    });
    client.on<TRound>("RoundResult", (result) => {
      if (!cancelled) handlersRef.current.onRoundResult?.(result);
    });
    client.on<number>("MatchEnded", (winner) => {
      if (!cancelled) handlersRef.current.onMatchEnded?.(winner);
    });
    client.on<DurniusStandingDto[]>("MatchStandings", (standings) => {
      if (!cancelled) handlersRef.current.onMatchStandings?.(standings);
    });

    client.onReconnecting(() => {
      if (!cancelled) setState(HubConnectionState.Reconnecting);
    });
    client.onReconnected(() => {
      if (cancelled) return;
      setState(HubConnectionState.Connected);
      // A disconnected session is dropped server-side, so start over.
      handlersRef.current.onReconnect?.();
      scheduleJoin(100);
    });
    client.onClose(() => {
      if (!cancelled) setState(HubConnectionState.Disconnected);
    });

    // Like the join, defer start so a StrictMode remount never opens a
    // throwaway connection.
    startTimer = window.setTimeout(() => {
      startTimer = undefined;
      void client
        .start()
        .then(() => {
          if (cancelled) {
            void client.stop();
            return;
          }
          setState(HubConnectionState.Connected);
          scheduleJoin();
        })
        .catch((err: unknown) => {
          if (!cancelled) setError(errorMessage(err));
        });
    });

    return () => {
      cancelled = true;
      if (startTimer !== undefined) window.clearTimeout(startTimer);
      if (joinTimer !== undefined) window.clearTimeout(joinTimer);
      if (clientRef.current === client) clientRef.current = null;
      void client.stop();
    };
  }, [token, game]);

  async function invoke(action: (client: GameClient) => Promise<void>) {
    const client = clientRef.current;
    if (!client) {
      setActionError("Not connected.");
      return;
    }
    setActionError(null);
    try {
      await action(client);
    } catch (err) {
      setActionError(errorMessage(err));
    }
  }

  return {
    state,
    connected: state === HubConnectionState.Connected,
    error,
    actionError,
    clearActionError: () => setActionError(null),
    join: () => invoke((client) => client.join(game)),
    leave: () => invoke((client) => client.leave()),
    move: (move: TMove) => invoke((client) => client.move(move)),
  };
}
