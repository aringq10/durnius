import type { CardDto } from "../lib/cards";
import { CARD_LABELS, SUIT_SYMBOLS } from "../lib/cards";

interface PlayingCardProps {
  card?: CardDto;
  /** Renders a card back instead of a face. */
  faceDown?: boolean;
  size?: "md" | "sm";
}

export default function PlayingCard({
  card,
  faceDown = false,
  size = "md",
}: PlayingCardProps) {
  const sizeClass = size === "sm" ? " pcard-sm" : "";

  if (faceDown || !card) {
    return <span className={`pcard pcard-back${sizeClass}`} aria-hidden="true" />;
  }

  const red = card.suit === "hearts" || card.suit === "diamonds";
  const corner = (
    <>
      <span>{CARD_LABELS[card.value]}</span>
      <span className="pcard-corner-suit">{SUIT_SYMBOLS[card.suit]}</span>
    </>
  );

  return (
    <span
      className={`pcard${red ? " pcard-red" : ""}${sizeClass}`}
      role="img"
      aria-label={`${card.value} of ${card.suit}`}
    >
      <span className="pcard-corner" aria-hidden="true">
        {corner}
      </span>
      <span className="pcard-pip" aria-hidden="true">
        {SUIT_SYMBOLS[card.suit]}
      </span>
      <span className="pcard-corner pcard-corner-bottom" aria-hidden="true">
        {corner}
      </span>
    </span>
  );
}
