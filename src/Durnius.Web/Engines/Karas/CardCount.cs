namespace Durnius.Web.Engines.Karas;

public readonly struct CardCount(int player1, int player2)
{
    public int Player1 { get; } = player1;
    public int Player2 { get; } = player2;
}
