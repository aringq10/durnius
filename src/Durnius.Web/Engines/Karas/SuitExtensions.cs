namespace Durnius.Web.Engines.Karas;

public static class SuitExtensions
{
    public static bool IsRed(this Suit suit) => suit is Suit.Hearts or Suit.Diamonds;
}
