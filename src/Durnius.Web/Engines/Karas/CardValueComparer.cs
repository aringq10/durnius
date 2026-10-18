namespace Durnius.Web.Engines.Karas;

/// <summary>Compares cards by value only, which is how War decides a round.</summary>
public sealed class CardValueComparer : IComparer<Card>
{
    public int Compare(Card? x, Card? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        return x.Value.CompareTo(y.Value);
    }
}