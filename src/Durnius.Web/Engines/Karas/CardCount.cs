namespace Durnius.Web.Engines.Karas;

public readonly struct CardCount
{
    public int Player1 { get; }
    public int Player2 { get; }

    public CardCount(int player1, int player2)
    {
        Player1 = player1;
        Player2 = player2;
    }
}
