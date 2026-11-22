using Durnius.Web.Features.Multiplayer;
using Xunit;

namespace Durnius.Web.Tests;

[CollectionDefinition("Coordination", DisableParallelization = true)]
public sealed class CoordinationCollection;

[Collection("Coordination")]
public sealed class CoordinationTests
{
    private static (LobbyManager Lobby, MatchManager Match, MultiplayerCoordinator Coordinator, SessionStore Sessions)
        NewSystem()
    {
        TestState.Reset();

        var registry = new MatchRegistry()
            .Register<DuelMatch>("duo")
            .Register<OtherDuelMatch>("other")
            .Register<TrioMatch>("trio");
        var matches = new MatchManager(registry);
        var lobby = new LobbyManager(registry, matches);
        return (lobby, matches, new MultiplayerCoordinator(lobby, matches), new SessionStore());
    }

    private static Session S(string id, string? username = null) => new(id, username ?? id);

    private static T? Effect<T>(CoordinationEffects? effects) where T : CoordinationEffect =>
        effects?.Items.OfType<T>().FirstOrDefault();

    private static async Task<(CoordinationEffects A, CoordinationEffects B)> FillDuo(
        LobbyManager lobby, Session a, Session b)
    {
        var first = await lobby.JoinAsync(a, "duo");
        var second = await lobby.JoinAsync(b, "duo");
        Assert.Null(first.Rejection);
        Assert.Null(second.Rejection);
        return (first.Effects!, second.Effects!);
    }

    [Fact]
    public async Task SameSessionCannotJoinTwoLobbies()
    {
        var (lobby, _, _, _) = NewSystem();
        var session = S("c1");

        var first = await lobby.JoinAsync(session, "duo");
        var second = await lobby.JoinAsync(session, "other");

        Assert.Null(first.Rejection);
        Assert.Equal("Already in another lobby", second.Rejection);
    }

    [Fact]
    public async Task ConcurrentJoinsClaimSessionOnlyOnce()
    {
        var (lobby, _, _, _) = NewSystem();
        var session = S("c1");

        var results = await Task.WhenAll(
            lobby.JoinAsync(session, "duo"),
            lobby.JoinAsync(session, "other"));

        Assert.Single(results, r => r.Rejection is null);
        Assert.Single(results, r => r.Rejection is not null);
    }

    [Fact]
    public async Task JoinIsRejectedWhenSessionIsInMatchOrLobby()
    {
        var (lobby, _, _, _) = NewSystem();
        var a = S("a");
        var b = S("b");

        await FillDuo(lobby, a, b);
        Assert.Equal("Already in a match", (await lobby.JoinAsync(a, "duo")).Rejection);


        var c = S("c", "duplicate");
        Assert.Null((await lobby.JoinAsync(c, "trio")).Rejection);
        Assert.Equal("Already in another lobby", (await lobby.JoinAsync(c, "trio")).Rejection);
        Assert.Equal("Already in this lobby", (await lobby.JoinAsync(S("d", "duplicate"), "trio")).Rejection);
        Assert.Equal("Unknown lobby", (await lobby.JoinAsync(S("e"), "missing")).Rejection);
    }

    [Fact]
    public async Task CapacityFillCreatesOneMatchMovesEveryoneAndResetsLobby()
    {
        var (lobby, matches, _, _) = NewSystem();
        var a = S("a", "Alice");
        var b = S("b", "Bob");

        var (effectsA, effectsB) = await FillDuo(lobby, a, b);

        Assert.Null(Effect<ResponseEffect>(effectsA));
        var response = Effect<ResponseEffect>(effectsB);
        Assert.NotNull(response);
        Assert.Equal(response.MatchId, Assert.Single(matches.ActiveMatchIds));
        Assert.Equal(["a", "b"], [.. TestState.LastOptions!.Select(p => p.ConnectionId)]);
        Assert.Single(response.Response.Public);
        Assert.Equal(response.MatchId, matches.GetMatchId("a"));
        Assert.Equal(response.MatchId, matches.GetMatchId("b"));

        // Nobody is left in the lobby, and moves are routed to the match.
        Assert.Null(await lobby.LeaveAsync(a));
        Assert.NotNull(await matches.ForwardMoveAsync(a, new object()));

        // The lobby is reusable immediately for the next game.
        var c = S("c");
        var d = S("d", "Dora");
        var nextFirst = await lobby.JoinAsync(c, "duo");
        var nextSecond = await lobby.JoinAsync(d, "duo");
        Assert.Equal(1, Effect<LobbyUpdateEffect>(nextFirst.Effects)!.Update.Count);
        var nextResponse = Effect<ResponseEffect>(nextSecond.Effects);
        Assert.NotNull(nextResponse);
        Assert.NotEqual(response.MatchId, nextResponse.MatchId);
    }

    [Fact]
    public async Task ConcurrentMovesForOneMatchAreSerialized()
    {
        var (lobby, matches, _, _) = NewSystem();
        TestState.Mode = TestMatchMode.DetectOverlap;

        var a = S("a");
        var b = S("b");
        await FillDuo(lobby, a, b);

        await Task.WhenAll(
            Task.Run(async () =>
            {
                for (var i = 0; i < 25; i++)
                {
                    await matches.ForwardMoveAsync(a, new object());
                }
            }),
            Task.Run(async () =>
            {
                for (var i = 0; i < 25; i++)
                {
                    await matches.ForwardMoveAsync(b, new object());
                }
            }));

        Assert.Equal(0, TestState.Overlaps);
        Assert.Equal(50, ((TestMatchBase)TestState.LastCreated!).Moves);
    }

    [Fact]
    public async Task OrdinaryMoveDoesNotRecordMatchHistory()
    {
        var (lobby, matches, _, _) = NewSystem();
        var a = S("a");
        var b = S("b");
        await FillDuo(lobby, a, b);

        var effects = await matches.ForwardMoveAsync(a, new object());

        Assert.Null(Effect<RecordMatchEffect>(effects));
    }

    [Fact]
    public async Task MoveAfterEndIsNoOpAndMatchIsCleanedUp()
    {
        var (lobby, matches, _, _) = NewSystem();
        TestState.Mode = TestMatchMode.EndOnFirstMove;

        var a = S("a");
        var b = S("b");
        await FillDuo(lobby, a, b);

        var ending = await matches.ForwardMoveAsync(a, new object());
        Assert.NotNull(Effect<ResponseEffect>(ending));
        var record = Effect<RecordMatchEffect>(ending);
        Assert.NotNull(record);
        Assert.Equal("duo", record.Game);
        Assert.Equal(["a", "b"], record.Result.Placements.Select(p => p.Player.Username));
        Assert.Equal([1, 2], record.Result.Placements.Select(p => p.Placement));
        Assert.Empty(matches.ActiveMatchIds);
        Assert.Null(matches.GetMatchId("a"));
        Assert.Null(matches.GetMatchId("b"));

        Assert.Null(await matches.ForwardMoveAsync(b, new object()));
        Assert.Equal(1, ((TestMatchBase)TestState.LastCreated!).Moves);
    }

    [Fact]
    public async Task LobbyDisconnectBroadcastsUpdateAndReleasesOwnership()
    {
        var (lobby, _, coordinator, _) = NewSystem();
        var session = S("a");
        await lobby.JoinAsync(session, "trio");

        var effects = await coordinator.DisconnectAsync(session);

        var update = Effect<LobbyUpdateEffect>(effects);
        Assert.NotNull(update);
        Assert.Equal("trio", update.LobbyName);
        Assert.Equal(0, update.Update.Count);
        Assert.Null(await lobby.LeaveAsync(session));
        Assert.Null((await lobby.JoinAsync(session, "duo")).Rejection);
    }

    [Fact]
    public async Task MatchDisconnectEndsMatchAndIsIdempotent()
    {
        var (lobby, matches, coordinator, _) = NewSystem();
        var a = S("a");
        var b = S("b");
        await FillDuo(lobby, a, b);

        var effects = await coordinator.DisconnectAsync(a);

        var response = Effect<ResponseEffect>(effects);
        Assert.NotNull(response);
        Assert.Contains(response.Response.Public, m => m.Method == "MatchEnded");
        var record = Effect<RecordMatchEffect>(effects);
        Assert.NotNull(record);
        Assert.Equal("duo", record.Game);
        Assert.Equal(["b", "a"], record.Result.Placements.Select(p => p.Player.Username));
        Assert.Equal([1, 2], record.Result.Placements.Select(p => p.Placement));
        Assert.Empty(matches.ActiveMatchIds);
        Assert.Null(matches.GetMatchId("a"));
        Assert.Null(matches.GetMatchId("b"));

        // Duplicate disconnect and moves after end are no-ops.
        Assert.Null(await coordinator.DisconnectAsync(a));
        Assert.Null(await coordinator.DisconnectAsync(b));
        Assert.Null(await matches.ForwardMoveAsync(b, new object()));
        Assert.Equal(1, ((TestMatchBase)TestState.LastCreated!).Disconnects);
    }

    private static async Task AssertOwnsAtMostOneRoom(
        LobbyManager lobby, MatchManager matches, Session session)
    {
        var leave = await lobby.LeaveAsync(session);
        var move = await matches.ForwardMoveAsync(session, new object());
        Assert.False(leave is not null && move is not null);
    }

    [Fact]
    public async Task JoinVersusDisconnectNeverLeavesSessionInTwoRooms()
    {
        for (var i = 0; i < 25; i++)
        {
            var (lobby, matches, coordinator, _) = NewSystem();
            var session = S("racer");
            await lobby.JoinAsync(session, "trio");

            using var start = new ManualResetEventSlim();
            var join = Task.Run(async () =>
            {
                start.Wait();
                return await lobby.JoinAsync(session, "duo");
            });
            var disconnect = Task.Run(async () =>
            {
                start.Wait();
                return await coordinator.DisconnectAsync(session);
            });
            start.Set();

            await Task.WhenAll(join, disconnect).WaitAsync(TimeSpan.FromSeconds(5));
            await AssertOwnsAtMostOneRoom(lobby, matches, session);
        }
    }

    [Fact]
    public async Task JoinVersusMatchTransitionNeverLeavesSessionInTwoRooms()
    {
        for (var i = 0; i < 25; i++)
        {
            var (lobby, matches, _, _) = NewSystem();
            var a = S("a");
            var b = S("b");
            var c = S("c");
            await lobby.JoinAsync(a, "trio");
            await lobby.JoinAsync(b, "trio");

            using var start = new ManualResetEventSlim();
            var fill = Task.Run(async () =>
            {
                start.Wait();
                return await lobby.JoinAsync(c, "trio");
            });
            var racer = Task.Run(async () =>
            {
                start.Wait();
                return await lobby.JoinAsync(a, "duo");
            });
            start.Set();

            await Task.WhenAll(fill, racer).WaitAsync(TimeSpan.FromSeconds(5));
            await AssertOwnsAtMostOneRoom(lobby, matches, a);
        }
    }

    [Fact]
    public void StaleDisconnectDoesNotRemoveNewerSession()
    {
        var (_, _, _, sessions) = NewSystem();
        var old = S("c1");
        sessions.Add(old);
        Assert.Throws<InvalidOperationException>(() => sessions.Add(S("c1")));

        Assert.True(sessions.Remove(old));
        var newer = S("c1");
        sessions.Add(newer);

        Assert.False(sessions.Remove(old));
        Assert.Same(newer, sessions.Get("c1"));
        Assert.Equal(1, sessions.Count);
    }
}
