import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { API_URL } from "./api";

/** Card suits and values as serialized by the Karas engine (lowercased enums). */
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
  | "jack"
  | "queen"
  | "king"
  | "ace";

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

/** LobbyUpdate payload (see LobbyHub.LobbyDto in Durnius.Web). */
export interface LobbyStateDto {
  capacity: number;
  count: number;
  players: string[];
}

/** MatchStarted payload. `you` is 1 or 2, matching player1/player2. */
export interface MatchStartedDto {
  you: 1 | 2;
  player1: string;
  player2: string;
  player1Count: number;
  player2Count: number;
}

/** DrawStatus payload: which players have drawn this round. */
export interface DrawStatusDto {
  player1Drawn: boolean;
  player2Drawn: boolean;
}

/** One extra pair of cards compared during a war. */
export interface WarPairDto {
  player1: CardDto;
  player2: CardDto;
}

/** RoundResult payload. `war` is empty when the first pair was decisive. */
export interface RoundResultDto {
  roundWinner: number;
  player1Card: CardDto;
  player2Card: CardDto;
  war: WarPairDto[];
  player1Count: number;
  player2Count: number;
  gameOver: boolean;
  gameWinner: number;
}

/** Builds an authenticated connection to the LobbyHub. */
export function createLobbyConnection(token: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/lobby`, { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
