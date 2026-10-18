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
    ten,
    jack,
    queen,
    king,
    ace
}

public sealed class Card : IEquatable<Card>, IComparable<Card>
{
    public Suit Suit { get; }
    public Value Value { get; }

    public Card(Suit suit, Value value)
    {
        Suit = suit;
        Value = value;
    }


    public bool Equals(Card? other)
    {
        return other is not null && Suit == other.Suit && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as Card);

    public override int GetHashCode() => HashCode.Combine(Suit, Value);


    public int CompareTo(Card? other)
    {
        if (other is null) return 1;

        int byValue = Value.CompareTo(other.Value);
        return byValue != 0 ? byValue : Suit.CompareTo(other.Suit);
    }

    public override string ToString() => $"{Value} of {Suit}";
}