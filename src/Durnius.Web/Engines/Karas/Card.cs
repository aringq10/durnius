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

public sealed class Card(Suit suit, Value value) : IEquatable<Card>, IComparable<Card>
{
    public Suit Suit { get; } = suit;
    public Value Value { get; } = value;

    public bool Equals(Card? other)
    {
        return other is not null && Suit == other.Suit && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as Card);

    public override int GetHashCode() => HashCode.Combine(Suit, Value);


    public int CompareTo(Card? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byValue = Value.CompareTo(other.Value);
        return byValue != 0 ? byValue : Suit.CompareTo(other.Suit);
    }

    public override string ToString() => $"{Value} of {Suit}";
}
