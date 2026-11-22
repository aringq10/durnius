namespace Durnius.Web.Features.Multiplayer;

// Both `MakeMove` and `Disconnect` are called atomically. A non-null result
// means the match ended; a null result means it is still running.
public interface IMatch
{
    (MatchResult? Result, Response Response) MakeMove(string connectionId, object move);

    (MatchResult? Result, Response Response) Disconnect(string connectionId);
}

public interface ICreatableMatch<TSelf> : IMatch where TSelf : ICreatableMatch<TSelf>
{
    static abstract (TSelf, Response) Create(MatchOptions options);

    // How many players participate in a match.
    static abstract int Capacity { get; }
}

public record MatchPlayer(string ConnectionId, string Username);

public record MatchOptions(IReadOnlyList<MatchPlayer> Players);

public record Response
{
    public IReadOnlyList<Message> Public { get; init; }
    public IReadOnlyDictionary<string, Message> Private { get; init; }

    public Response(
        IReadOnlyList<Message>? publicMessage = null,
        IReadOnlyDictionary<MatchPlayer, Message>? privateByPlayer = null)
    {
        Public = publicMessage ?? [];
        Private = privateByPlayer?.ToDictionary(kv => kv.Key.ConnectionId, kv => kv.Value)
                  ?? [];
    }
}

public record Message(string Method, object Payload);
