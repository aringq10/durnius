namespace Durnius.Web.Features.Multiplayer;

/// <summary>A player's final position in a finished match; 1 is the winner.</summary>
public sealed record MatchPlacement(MatchPlayer Player, int Placement);

/// <summary>
/// The final standings of a finished match. A non-null result is what signals
/// match end to the coordination layer.
/// </summary>
public sealed record MatchResult(IReadOnlyList<MatchPlacement> Placements);
