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

public abstract class TestMatchBase : IMatch
{
    private readonly Lock _gate = new();
    private int _inFlight;
    private bool _ended;

    protected TestMatchBase(MatchOptions options)
    {
        Players = options.Players;
    }

    public IReadOnlyList<MatchPlayer> Players { get; }

    public int Moves { get; private set; }
    public int Disconnects { get; private set; }

    public (bool Ended, Response Response) MakeMove(string connectionId, object move)
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
                    return (false, new());
                }
                if (TestState.Mode == TestMatchMode.EndOnFirstMove)
                {
                    _ended = true;
                    return (true, new([new Message("MatchEnded", 1)]));
                }
                return (false, new([new Message("MoveAccepted", connectionId)]));
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public (bool Ended, Response Response) Disconnect(string connectionId)
    {
        lock (_gate)
        {
            Disconnects++;
            if (_ended)
            {
                return (false, new());
            }
            _ended = true;
            return (true, new([new Message("MatchEnded", 2)]));
        }
    }
}

public sealed class DuelMatch : TestMatchBase, ICreatableMatch<DuelMatch>
{
    public static int Capacity => 2;

    public DuelMatch(MatchOptions options) : base(options) { }

    public static (DuelMatch, Response) Create(MatchOptions options)
    {
        var match = new DuelMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}

public sealed class OtherDuelMatch : TestMatchBase, ICreatableMatch<OtherDuelMatch>
{
    public static int Capacity => 2;

    public OtherDuelMatch(MatchOptions options) : base(options) { }

    public static (OtherDuelMatch, Response) Create(MatchOptions options)
    {
        var match = new OtherDuelMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}

public sealed class TrioMatch : TestMatchBase, ICreatableMatch<TrioMatch>
{
    public static int Capacity => 3;

    public TrioMatch(MatchOptions options) : base(options) { }

    public static (TrioMatch, Response) Create(MatchOptions options)
    {
        var match = new TrioMatch(options);
        TestState.Register(match, options);
        return (match, TestState.InitialResponse(options));
    }
}
