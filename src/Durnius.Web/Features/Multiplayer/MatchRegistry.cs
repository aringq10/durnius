using Durnius.Web.Engines.Karas;

namespace Durnius.Web.Features.Multiplayer;

public enum Game
{
    Karas
}

public static class MatchRegistry
{
    public static (IMatch Match, Response Response) GetMatch(Game name, MatchOptions options)
    {
        return name switch
        {
            Game.Karas => KarasMatch.Create(options),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }
}
