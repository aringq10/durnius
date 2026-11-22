using Durnius.Web.Features.Multiplayer;

namespace Durnius.Web.Tests;

public enum TestMatchMode
{
    Normal,
    EndOnFirstMove,
    DetectOverlap
}

public static class TestState
{
    public static TestMatchMode Mode { get; set; }
    public static int Overlaps;
    public static IMatch? LastCreated;
    public static IReadOnlyList<MatchPlayer>? LastOptions;

    public static void Reset()
    {
        Mode = TestMatchMode.Normal;
        Overlaps = 0;
        LastCreated = null;
        LastOptions = null;
    }

    internal static void Register(IMatch match, MatchOptions options)
    {
        LastCreated = match;
        LastOptions = options.Players;
    }

    internal static Response InitialResponse(MatchOptions options) =>
        new([new Message("MatchStarted", options.Players.Count)]);
}

public abstract class TestMatchBase(MatchOptions options) : IMatch
{
    private readonly Lock _gate = new();
    private readonly IReadOnlyList<MatchPlayer> _players = options.Players;
    private int _inFlight;
    private bool _ended;

    public int Moves { get; private set; }
    public int Disconnects { get; private set; }

    public (MatchResult? Result, Response Response) MakeMove(string connectionId, object move)
    {
        if (Interlocked.Increment(ref _inFlight) > 1)
        {
            Interlocked.Increment(ref TestState.Overlaps);
        }

        try
        {
            lock (_gate)
            {
                Moves++;
                if (TestState.Mode == TestMatchMode.DetectOverlap)
                {
                    Thread.Sleep(10);
                }
                if (_ended)
                {
                    return (null, new());
                }
                if (TestState.Mode == TestMatchMode.EndOnFirstMove)
                {
                    _ended = true;
                    return (ResultFromWinner(1), new([new Message("MatchEnded", 1)]));
                }
                return (null, new([new Message("MoveAccepted", connectionId)]));
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public (MatchResult? Result, Response Response) Disconnect(string connectionId)
    {
        lock (_gate)
        {
            Disconnects++;
            if (_ended)
            {
                return (null, new());
            }
            _ended = true;
            return (ResultFromWinner(2), new([new Message("MatchEnded", 2)]));
        }
    }

    private MatchResult ResultFromWinner(int winner)
    {
        var winnerPlayer = _players[winner - 1];
        var placements = new List<MatchPlacement> { new(winnerPlayer, 1) };
        placements.AddRange(_players
            .Where(player => player != winnerPlayer)
            .Select((player, index) => new MatchPlacement(player, index + 2)));
        return new MatchResult(placements);
    }
}

public sealed class DuelMatch : TestMatchBase, ICreatableMatch<DuelMatch>
{
    private DuelMatch(MatchOptions options) : base(options) { }

    public static int Capacity => 2;

    public static (DuelMatch, Response) Create(MatchOptions options)
    {
        var match = new DuelMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}

public sealed class OtherDuelMatch : TestMatchBase, ICreatableMatch<OtherDuelMatch>
{
    private OtherDuelMatch(MatchOptions options) : base(options) { }

    public static int Capacity => 2;

    public static (OtherDuelMatch, Response) Create(MatchOptions options)
    {
        var match = new OtherDuelMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}

public sealed class TrioMatch : TestMatchBase, ICreatableMatch<TrioMatch>
{
    private TrioMatch(MatchOptions options) : base(options) { }

    public static int Capacity => 3;

    public static (TrioMatch, Response) Create(MatchOptions options)
    {
        var match = new TrioMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}
