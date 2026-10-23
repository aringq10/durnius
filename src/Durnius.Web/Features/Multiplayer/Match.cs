namespace Durnius.Web.Features.Multiplayer;

public interface IMatch<TSelf> where TSelf : IMatch<TSelf>
{
    static abstract (TSelf, Response) Create(MatchOptions options);

    Response MakeMove(string connectionId, object move);

    Response Disconnect(string connectionId);

    bool IsRunning { get; }
}

public record MatchOptions(List<MatchPlayer> Players);

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
