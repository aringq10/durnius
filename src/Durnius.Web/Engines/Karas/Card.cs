namespace Durnius.Web.Engines.Karas;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Value
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public class Card(Suit suit, Value value)
{
    public Suit Suit { get; } = suit;
    public Value Value { get; } = value;
}
