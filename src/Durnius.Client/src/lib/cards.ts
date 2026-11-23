// Card types and rules shared by both games. Cards travel over the wire as
// lowercase enum names, e.g. { suit: "hearts", value: "queen" }.

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

export function cardKey(card: CardDto): string {
  return `${card.suit}-${card.value}`;
}

export function sameCard(
  left: CardDto | null,
  right: CardDto | null
): boolean {
  return left !== null && right !== null && cardKey(left) === cardKey(right);
}

export function rank(value: CardValue): number {
  return CARD_VALUES.indexOf(value);
}

/** Sorts a hand by suit then rank, keeping trumps together at the end. */
export function sortHand(hand: CardDto[], trump?: Suit): CardDto[] {
  const suitOrder = (suit: Suit) =>
    suit === trump ? SUITS.length : SUITS.indexOf(suit);
  return [...hand].sort(
    (left, right) =>
      suitOrder(left.suit) - suitOrder(right.suit) ||
      rank(left.value) - rank(right.value)
  );
}

/** Same rule as the Durnius engine: higher of the same suit, or trump over a non-trump. */
export function canBeat(
  attack: CardDto,
  defense: CardDto,
  trump: Suit
): boolean {
  if (attack.suit === defense.suit) {
    return rank(defense.value) > rank(attack.value);
  }
  return defense.suit === trump && attack.suit !== trump;
}
