import type { CardDto } from "../lib/hub";
import { CARD_LABELS, SUIT_SYMBOLS } from "../lib/hub";

interface PlayingCardProps {
  card?: CardDto;
  /** Renders a card back instead of a face. */
  faceDown?: boolean;
}

export default function PlayingCard({ card, faceDown = false }: PlayingCardProps) {
  if (faceDown || !card) {
    return <span className="playing-card playing-card-back" aria-hidden="true" />;
  }

  const red = card.suit === "hearts" || card.suit === "diamonds";
  return (
    <span className={`playing-card${red ? " playing-card-red" : ""}`}>
      {CARD_LABELS[card.value]}
      {SUIT_SYMBOLS[card.suit]}
    </span>
  );
}
