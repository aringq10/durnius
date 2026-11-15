namespace Durnius.Web.Features.Multiplayer;

public interface IMatch
{
    bool IsRunning { get; }

    IReadOnlyList<MatchPlayer> Players { get; }

    Response MakeMove(string connectionId, object move);

    Response Disconnect(string connectionId);
}

public interface ICreatableMatch<TSelf> : IMatch where TSelf : ICreatableMatch<TSelf>
{
    static abstract (TSelf, Response) Create(MatchOptions options);
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
