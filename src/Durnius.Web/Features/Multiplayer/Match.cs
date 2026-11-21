namespace Durnius.Web.Features.Multiplayer;

// Both `MakeMove` and `Disconnect` are expected to be inclusively atomic, thus locked.
// On the first occurence that `Ended` is returned as True, a non-empty
// `Response` may be returned. On subsequent such occurences, `Ended` is
// expected to be False and `Response` to contain no messages;
public interface IMatch
{
    IReadOnlyList<MatchPlayer> Players { get; }

    (bool Ended, Response Response) MakeMove(string connectionId, object move);

    (bool Ended, Response Response) Disconnect(string connectionId);
}

public interface ICreatableMatch<TSelf> : IMatch where TSelf : ICreatableMatch<TSelf>
{
    static abstract (TSelf, Response) Create(MatchOptions options);

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
