using System.Reflection;
using System.Text.Json;
using Durnius.Web.Features.Multiplayer;

namespace Durnius.Web.Engines.Durnius;

public sealed record DurniusCardDto
{
    public string Suit { get; init; }
    public string Value { get; init; }

    public DurniusCardDto(Card card)
    {
        Suit = card.Suit.ToString().ToLowerInvariant();
        Value = card.Value.ToString().ToLowerInvariant();
    }

    public DurniusCardDto(string suit, string value)
    {
        Suit = suit;
        Value = value;
    }
}

public sealed record DurniusPlayedCardDto
{
    public int PlayerIndex { get; init; }
    public DurniusCardDto AttackCard { get; init; }
    public DurniusCardDto? DefenseCard { get; init; }

    public DurniusPlayedCardDto(PlayedCard played)
    {
        PlayerIndex = played.PlayerIndex;
        AttackCard = new DurniusCardDto(played.AttackCard);
        DefenseCard = played.DefenseCard is null ? null : new DurniusCardDto(played.DefenseCard);
    }
}

public sealed record DurniusRoundResultDto
{
    public int Attacker { get; init; }
    public int Defender { get; init; }
    public bool DefenderTookCards { get; init; }
    public IReadOnlyList<DurniusPlayedCardDto> CardsOnTable { get; init; }
    public IReadOnlyList<int> PlayerCardCounts { get; init; }
    public int NextAttacker { get; init; }
    public IReadOnlyList<int> FinishedPlayers { get; init; }
    public IReadOnlyList<int> PlayerPlacements { get; init; }
    public bool GameOver { get; init; }
    public int? GameWinner { get; init; }
    public int? GameLoser { get; init; }

    public DurniusRoundResultDto(RoundResult round)
    {
        Attacker = round.Attacker;
        Defender = round.Defender;
        DefenderTookCards = round.DefenderTookCards;
        CardsOnTable = [.. round.CardsOnTable.Select(card => new DurniusPlayedCardDto(card))];
        PlayerCardCounts = round.PlayerCardCounts;
        NextAttacker = round.NextAttacker;
        FinishedPlayers = round.FinishedPlayers;
        PlayerPlacements = round.PlayerPlacements;
        GameOver = round.GameOver;
        GameWinner = round.GameWinner;
        GameLoser = round.GameLoser;
    }
}

public sealed record DurniusStandingDto(int PlayerIndex, string Username, int Placement);

/// <summary>The state sent privately to each player after a move; only <see cref="Hand"/> differs.</summary>
public sealed record DurniusStateDto
{
    public required int You { get; init; }
    public required IReadOnlyList<string> Players { get; init; }
    public required IReadOnlyList<DurniusCardDto> Hand { get; init; }
    public required IReadOnlyList<int> CardCounts { get; init; }
    public required IReadOnlyList<DurniusPlayedCardDto> Table { get; init; }
    public required int DeckCount { get; init; }
    public required string TrumpSuit { get; init; }
    public required DurniusCardDto TrumpCard { get; init; }
    public required int CurrentAttacker { get; init; }
    public required int CurrentDefender { get; init; }
    public DurniusRoundResultDto? LastRound { get; init; }
    public required IReadOnlyList<int> FinishedPlayers { get; init; }
    public required IReadOnlyList<int> PlayerPlacements { get; init; }
    public bool GameOver { get; init; }
    public int? GameWinner { get; init; }
    public int? GameLoser { get; init; }
}

/// <summary>
/// The accepted move shape. SignalR delivers <c>object</c> moves as
/// <see cref="JsonElement"/>, but plain objects and dictionaries work as well.
/// Type: attack, defend, transfer, pass or take.
/// </summary>
public sealed record DurniusMove(
    string Type,
    DurniusCardDto? Card = null,
    DurniusCardDto? AttackCard = null,
    DurniusCardDto? DefenseCard = null);

/// <summary>
/// SignalR wrapper around the Durnius engine. Player indices are zero-based and
/// follow the order of <see cref="MatchOptions.Players"/>. Each accepted move
/// sends a private <c>GameState</c> (including only that player's hand) to every
/// player, plus public <c>RoundResult</c> and end-of-match messages when relevant.
/// </summary>
public class DurniusMatch : ICreatableMatch<DurniusMatch>
{
    private readonly GameEngine _game;
    private readonly IReadOnlyList<MatchPlayer> _players;
    private bool _ended;

    private DurniusMatch(IReadOnlyList<MatchPlayer> players)
    {
        _players = players;
        _game = new GameEngine(players.Count);
    }

    public static int Capacity => 2;

    public static (DurniusMatch, Response) Create(MatchOptions options)
    {
        if (options.Players.Count is < 2 or > 6)
        {
            throw new ArgumentException("Durnius needs 2 to 6 players.", nameof(options));
        }

        var match = new DurniusMatch(options.Players.ToList());
        return (match, match.StateResponse("MatchStarted"));
    }

    public (MatchResult? Result, Response Response) MakeMove(string connectionId, object move)
    {
        if (_ended || _game.GameOver)
        {
            return (null, new());
        }

        var playerIndex = IndexOf(connectionId);
        if (playerIndex < 0)
        {
            return (null, new());
        }

        ParsedMove parsed;
        RoundResult? round;
        try
        {
            parsed = ParseMove(move);
            round = parsed.Kind switch
            {
                MoveKind.Attack when parsed.Card is { } card => _game.PlayAttack(playerIndex, card),
                MoveKind.Defend when parsed.AttackCard is { } attack && parsed.DefenseCard is { } defense
                    => _game.Defend(playerIndex, attack, defense),
                MoveKind.Transfer when parsed.Card is { } card => _game.Transfer(playerIndex, card),
                MoveKind.Pass => _game.PassAttack(playerIndex),
                MoveKind.Take => _game.TakeCards(playerIndex),
                _ => null
            };
        }
        catch (InvalidOperationException)
        {
            // Wrong turn, unplayable card, etc. Invalid moves do not change state.
            return (null, new());
        }
        catch (ArgumentException)
        {
            return (null, new());
        }

        if (round is null && parsed.Kind == MoveKind.Unknown)
        {
            return (null, new());
        }

        if (!_game.GameOver)
        {
            return (null, StateResponse("GameState", round));
        }

        _ended = true;
        return (ResultFromPlacements(_game.PlayerPlacements), StateResponse("GameState", round, EndMessages(Standings())));
    }

    public (MatchResult? Result, Response Response) Disconnect(string connectionId)
    {
        if (_ended)
        {
            return (null, new());
        }

        var playerIndex = IndexOf(connectionId);
        if (playerIndex < 0)
        {
            return (null, new());
        }

        _ended = true;
        var placements = _game.PlayerPlacements;
        var remaining = Enumerable.Range(0, _players.Count)
            .Where(index => index != playerIndex)
            .OrderBy(index => placements[index])
            .Append(playerIndex)
            .ToArray();

        var result = new MatchResult(remaining
            .Select((index, order) => new MatchPlacement(_players[index], order + 1))
            .ToArray());
        var standings = remaining
            .Select((index, order) => new DurniusStandingDto(index, _players[index].Username, order + 1))
            .ToArray();

        return (result, new Response(publicMessage: EndMessages(standings)));
    }

    private int IndexOf(string connectionId)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].ConnectionId == connectionId)
            {
                return i;
            }
        }

        return -1;
    }

    private Response StateResponse(
        string method,
        RoundResult? round = null,
        IReadOnlyList<Message>? extraPublic = null)
    {
        var publicMessages = new List<Message>();
        if (round is not null)
        {
            publicMessages.Add(new Message("RoundResult", new DurniusRoundResultDto(round)));
        }
        if (extraPublic is not null)
        {
            publicMessages.AddRange(extraPublic);
        }

        var privateByPlayer = new Dictionary<MatchPlayer, Message>();
        for (var i = 0; i < _players.Count; i++)
        {
            privateByPlayer[_players[i]] = new Message(method, StateDto(i, round));
        }

        return new Response(publicMessages, privateByPlayer);
    }

    private DurniusStateDto StateDto(int playerIndex, RoundResult? round = null) => new()
    {
        You = playerIndex,
        Players = [.. _players.Select(player => player.Username)],
        Hand = [.. _game.PlayerCards(playerIndex).Select(card => new DurniusCardDto(card))],
        CardCounts = _game.CardCounts(),
        Table = [.. _game.CardsOnTable().Select(card => new DurniusPlayedCardDto(card))],
        DeckCount = _game.DeckCount,
        TrumpSuit = _game.TrumpSuit.ToString().ToLowerInvariant(),
        TrumpCard = new DurniusCardDto(_game.TrumpCard),
        CurrentAttacker = _game.CurrentAttacker,
        CurrentDefender = _game.CurrentDefender,
        LastRound = round is null ? null : new DurniusRoundResultDto(round),
        FinishedPlayers = _game.FinishedPlayers,
        PlayerPlacements = _game.PlayerPlacements,
        GameOver = _game.GameOver,
        GameWinner = _game.GameWinner,
        GameLoser = _game.GameLoser
    };

    private IReadOnlyList<Message> EndMessages(IReadOnlyList<DurniusStandingDto> standings) =>
    [
        new Message("MatchEnded", standings[0].PlayerIndex),
        new Message("MatchStandings", standings)
    ];

    private DurniusStandingDto[] Standings()
    {
        var placements = _game.PlayerPlacements;
        return [.. Enumerable.Range(0, _players.Count)
            .OrderBy(index => placements[index])
            .Select(index => new DurniusStandingDto(index, _players[index].Username, placements[index]))];
    }

    private MatchResult ResultFromPlacements(IReadOnlyList<int> placements) =>
        new(Enumerable.Range(0, _players.Count)
            .OrderBy(index => placements[index])
            .Select((index, order) => new MatchPlacement(_players[index], order + 1))
            .ToArray());

    private enum MoveKind
    {
        Unknown,
        Attack,
        Defend,
        Transfer,
        Pass,
        Take
    }

    private sealed record ParsedMove(
        MoveKind Kind,
        Card? Card = null,
        Card? AttackCard = null,
        Card? DefenseCard = null);

    private static ParsedMove ParseMove(object? move)
    {
        if (move is null)
        {
            return new ParsedMove(MoveKind.Unknown);
        }
        if (move is string text)
        {
            return new ParsedMove(ParseKind(text));
        }

        var fields = Normalize(move);
        if (fields.Count == 0)
        {
            return new ParsedMove(MoveKind.Unknown);
        }

        var kind = MoveKind.Unknown;
        foreach (var name in new[] { "type", "action", "move", "method", "kind" })
        {
            if (fields.TryGetValue(name, out var rawKind) && ParseKind(rawKind) is var parsed && parsed != MoveKind.Unknown)
            {
                kind = parsed;
                break;
            }
        }

        if (kind == MoveKind.Defend)
        {
            TryGetCard(fields, out var attackCard, "attackCard", "attack", "card", "playedCard");
            TryGetCard(fields, out var defenseCard, "defenseCard", "defendCard", "defense", "defendingCard", "with", "card2");
            return new ParsedMove(kind, AttackCard: attackCard, DefenseCard: defenseCard);
        }

        TryGetCard(fields, out var card, "card", "attackCard", "attack", "playedCard");
        if (card is null && fields.ContainsKey("suit"))
        {
            TryParseCard(fields, out card);
        }

        return kind switch
        {
            MoveKind.Attack or MoveKind.Transfer => new ParsedMove(kind, card),
            _ => new ParsedMove(kind)
        };
    }

    private static MoveKind ParseKind(object? value)
    {
        var text = AsString(value)?.ToLowerInvariant()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return text switch
        {
            "attack" or "playattack" or "play" or "open" or "attackcard" => MoveKind.Attack,
            "defend" or "defense" or "defendcard" => MoveKind.Defend,
            "transfer" => MoveKind.Transfer,
            "pass" or "passattack" => MoveKind.Pass,
            "take" or "takecards" or "pickup" => MoveKind.Take,
            _ => MoveKind.Unknown
        };
    }

    private static void TryGetCard(
        IReadOnlyDictionary<string, object?> fields,
        out Card? card,
        params string[] names)
    {
        card = null;
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) && TryParseCard(value, out card))
            {
                return;
            }
        }
    }

    private static bool TryParseCard(object? value, out Card card)
    {
        card = null!;
        if (value is null)
        {
            return false;
        }
        if (value is Card existing)
        {
            card = existing;
            return true;
        }

        var fields = Normalize(value);
        if (fields.TryGetValue("suit", out var suit)
            && fields.TryGetValue("value", out var face)
            && TryParseEnum(suit, out Suit parsedSuit)
            && TryParseEnum(face, out Value parsedValue))
        {
            card = new Card(parsedSuit, parsedValue);
            return true;
        }

        // Also accept notation such as "clubs-six" for a whole card.
        var parts = AsString(value)?
            .Replace('-', ' ')
            .Replace(':', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts?.Length == 2
            && TryParseEnum(parts[0], out parsedSuit)
            && TryParseEnum(parts[1], out parsedValue))
        {
            card = new Card(parsedSuit, parsedValue);
            return true;
        }

        return false;
    }

    private static bool TryParseEnum<T>(object? value, out T result) where T : struct, Enum
    {
        result = default;
        var text = AsString(value)?.Trim();
        if (text is null)
        {
            return false;
        }
        if (Enum.TryParse(text, ignoreCase: true, out result) && Enum.IsDefined(result))
        {
            return true;
        }
        if (int.TryParse(text, out var number) && Enum.IsDefined(typeof(T), number))
        {
            result = (T)(object)number;
            return true;
        }

        return false;
    }

    private static string? AsString(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } json => json.GetRawText(),
        string text => text,
        null => null,
        _ => value.ToString()
    };

    private static Dictionary<string, object?> Normalize(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            return json.EnumerateObject()
                .ToDictionary(property => property.Name, property => (object?)property.Value, StringComparer.OrdinalIgnoreCase);
        }
        if (value is IDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        if (value is null)
        {
            return [];
        }

        return value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, property => (object?)property.GetValue(value), StringComparer.OrdinalIgnoreCase);
    }
}
