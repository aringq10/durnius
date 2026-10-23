using Durnius.Web.Engines.Karas;
using Durnius.Web.Features.Multiplayer;

namespace Durnius.Web.Features.Multiplayer;

public static class MatchRegistry
{
    public static (IMatch Match, Response Response)? GetMatch(string name, MatchOptions options)
    {
        return name switch
        {
            "durnius" => KarasMatch.Create(options),
            _         => null
        };
    }
}
