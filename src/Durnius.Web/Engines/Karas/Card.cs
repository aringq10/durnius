namespace Durnius.Web.Engines.Karas;

public enum Suit
{
    hearts,
    diamonds,
    clubs,
    spades
}

public enum Value
{
    two = 2,
    three,
    four,
    five,
    six,
    seven,
    eight,
    nine,
    jack,
    queen,
    king,
    ace
}

public class Card
{
    public Suit Suit { get; }
    public Value Value { get; }

    public Card(Suit suit, Value value)
    {
        Suit = suit;
        Value = value;
    }
}