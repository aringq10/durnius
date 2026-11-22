namespace Durnius.Web.Engines.Durnius;

public sealed record PlayedCard(int PlayerIndex, Card AttackCard, Card? DefenseCard);

public sealed class RoundResult
{
    /// <summary>The player who opened the round (main attacker for draw order).</summary>
    public required int Attacker { get; init; }
    public required int Defender { get; init; }
    public required bool DefenderTookCards { get; init; }
    public required IReadOnlyList<PlayedCard> CardsOnTable { get; init; }
    public required IReadOnlyList<int> PlayerCardCounts { get; init; }
    public required int NextAttacker { get; init; }
    /// <summary>Players who emptied their hands, in finishing order (best first).</summary>
    public required IReadOnlyList<int> FinishedPlayers { get; init; }
    /// <summary>Placement by player index; unfinished players are tied for last place.</summary>
    public required IReadOnlyList<int> PlayerPlacements { get; init; }
    public required bool GameOver { get; init; }
    /// <summary>First player to empty their hand, i.e. the best-placed player.</summary>
    public required int? GameWinner { get; init; }
    /// <summary>The last player holding cards when the game ended; null in a draw.</summary>
    public required int? GameLoser { get; init; }
}

public class GameEngine
{
    private const int InitialHandSize = 6;
    private const int MaxCardsOnTable = 6;

    private readonly Player[] _players;
    private readonly Deck _deck;
    private readonly int _initialHandSize;
    private readonly List<PlayedCard> _table = [];
    private readonly HashSet<int> _passedAttackers = [];
    private readonly List<int> _finishedPlayers = [];

    private readonly Card _trumpCard;
    private int _mainAttacker;
    private int _maxCardsOnTable;
    private bool _gameOver;
    private int? _gameWinner;
    private int? _gameLoser;

    public GameEngine(int playerCount, int? seed = null)
    {
        if (playerCount is < 2 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), "Durnius supports 2 to 6 players.");
        }

        _players = new Player[playerCount];
        for (var i = 0; i < _players.Length; i++)
        {
            _players[i] = new Player();
        }

        _initialHandSize = InitialHandSize;

        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        _deck = new Deck();
        _deck.Shuffle(random);
        _trumpCard = _deck.BottomCard!;

        for (var cardNumber = 0; cardNumber < InitialHandSize; cardNumber++)
        {
            foreach (var player in _players)
            {
                player.AddCard(_deck.DrawCard());
            }
        }

        TrumpSuit = _trumpCard.Suit;
        CurrentAttacker = FindStartingPlayer();
        _mainAttacker = CurrentAttacker;
        CurrentDefender = NextPlayerWithCards(CurrentAttacker);
        _maxCardsOnTable = Math.Min(MaxCardsOnTable, _players[CurrentDefender].CardCount());
    }

    internal GameEngine(
        IReadOnlyList<IReadOnlyList<Card>> hands,
        IReadOnlyList<Card> talon,
        Suit trumpSuit,
        int attacker,
        int handSize = InitialHandSize)
    {
        if (hands.Count is < 2 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(hands), "Durnius supports 2 to 6 players.");
        }

        if (attacker < 0 || attacker >= hands.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(attacker));
        }

        if (handSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(handSize));
        }

        _players = new Player[hands.Count];
        for (var i = 0; i < hands.Count; i++)
        {
            var player = new Player();
            foreach (var card in hands[i])
            {
                player.AddCard(card);
            }
            _players[i] = player;
        }

        _deck = new Deck(talon);
        _initialHandSize = handSize;
        _trumpCard = talon.Count == 0 ? new Card(trumpSuit, Value.Two) : talon[^1];
        TrumpSuit = trumpSuit;
        CurrentAttacker = attacker;
        _mainAttacker = attacker;
        CurrentDefender = NextPlayerWithCards(attacker);
        _maxCardsOnTable = Math.Min(MaxCardsOnTable, _players[CurrentDefender].CardCount());
    }

    public int PlayerCount => _players.Length;
    public Suit TrumpSuit { get; }
    public Card TrumpCard => _trumpCard;
    public int DeckCount => _deck.Count();
    public int CurrentAttacker { get; private set; }
    public int CurrentDefender { get; private set; }
    public bool GameOver => _gameOver;
    public int? GameWinner => _gameWinner;
    public int? GameLoser => _gameLoser;
    public IReadOnlyList<int> FinishedPlayers => _finishedPlayers.ToArray();

    public IReadOnlyList<int> PlayerPlacements
    {
        get
        {
            var placements = new int[PlayerCount];
            for (var i = 0; i < _finishedPlayers.Count; i++)
            {
                placements[_finishedPlayers[i]] = i + 1;
            }

            var next = _finishedPlayers.Count + 1;
            for (var i = 0; i < PlayerCount; i++)
            {
                if (!IsFinished(i))
                {
                    placements[i] = next++;
                }
            }

            return placements;
        }
    }

    public IReadOnlyList<Card> PlayerCards(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        return _players[playerIndex].Cards;
    }

    public IReadOnlyList<int> CardCounts() =>
        _players.Select(player => player.CardCount()).ToArray();

    public IReadOnlyList<PlayedCard> CardsOnTable() => _table.ToArray();

    public RoundResult? PlayAttack(int playerIndex, Card card)
    {
        EnsureGameInProgress();
        ValidatePlayerIndex(playerIndex);
        ArgumentNullException.ThrowIfNull(card);

        if (_table.Count == 0)
        {
            if (playerIndex != CurrentAttacker)
            {
                throw new InvalidOperationException("Only the current attacker can open the round.");
            }
        }
        else
        {
            EnsureCanAddAttack(playerIndex);
            if (!_table.Any(played => played.AttackCard.Value == card.Value
                || played.DefenseCard?.Value == card.Value))
            {
                throw new InvalidOperationException("An added card must match a rank already on the table.");
            }
        }

        if (_table.Count >= _maxCardsOnTable)
        {
            throw new InvalidOperationException("The defender cannot be attacked with more cards than they had.");
        }

        RemoveFromPlayer(playerIndex, card);
        _table.Add(new PlayedCard(playerIndex, card, null));
        _passedAttackers.Clear();
        return null;
    }

    public RoundResult? Defend(int playerIndex, Card attackCard, Card defenseCard)
    {
        EnsureGameInProgress();
        ValidatePlayerIndex(playerIndex);
        ArgumentNullException.ThrowIfNull(attackCard);
        ArgumentNullException.ThrowIfNull(defenseCard);

        if (playerIndex != CurrentDefender)
        {
            throw new InvalidOperationException("Only the current defender can defend.");
        }

        var attackIndex = _table.FindIndex(played =>
            played.AttackCard.Equals(attackCard) && played.DefenseCard is null);
        if (attackIndex < 0)
        {
            throw new InvalidOperationException("That attack card is not waiting to be defended.");
        }

        if (!CanBeat(attackCard, defenseCard))
        {
            throw new InvalidOperationException("The defense card cannot beat the attack card.");
        }

        RemoveFromPlayer(playerIndex, defenseCard);
        _table[attackIndex] = _table[attackIndex] with { DefenseCard = defenseCard };
        return TryCompleteSuccessfulRound();
    }

    public RoundResult? Transfer(int playerIndex, Card card)
    {
        EnsureGameInProgress();
        ValidatePlayerIndex(playerIndex);
        ArgumentNullException.ThrowIfNull(card);

        if (playerIndex != CurrentDefender)
        {
            throw new InvalidOperationException("Only the current defender can transfer the attack.");
        }

        if (_table.Any(played => played.DefenseCard is not null))
        {
            throw new InvalidOperationException("An attack cannot be transferred after defending has started.");
        }

        // Passing is a defense option, so an attacker's earlier pass must not block it.
        EnsureCanAddAttack(playerIndex, defenderMayAct: true, checkPasses: false);

        if (!_table.Any(played => played.AttackCard.Value == card.Value))
        {
            throw new InvalidOperationException("A transfer card must match a rank already on the table.");
        }

        var nextDefender = NextPlayerWithCards(CurrentDefender);
        if (nextDefender == CurrentDefender)
        {
            throw new InvalidOperationException("There is no other player who can defend the transferred attack.");
        }

        var newLimit = Math.Min(MaxCardsOnTable, _players[nextDefender].CardCount());
        if (_table.Count >= newLimit)
        {
            throw new InvalidOperationException("The next defender does not have enough cards to take the attack.");
        }

        RemoveFromPlayer(playerIndex, card);
        _table.Add(new PlayedCard(playerIndex, card, null));
        CurrentAttacker = playerIndex;
        CurrentDefender = nextDefender;
        _maxCardsOnTable = newLimit;
        _passedAttackers.Clear();
        return null;
    }

    public RoundResult? PassAttack(int playerIndex)
    {
        EnsureGameInProgress();
        ValidatePlayerIndex(playerIndex);

        if (_table.Count == 0)
        {
            throw new InvalidOperationException("The attacker must open the round before a player can pass.");
        }

        if (playerIndex == CurrentDefender || IsFinished(playerIndex)
            || _players[playerIndex].CardCount() == 0)
        {
            throw new InvalidOperationException("Only an attacking player with cards can pass.");
        }

        if (!_passedAttackers.Add(playerIndex))
        {
            throw new InvalidOperationException("That player has already passed.");
        }

        return TryCompleteSuccessfulRound();
    }

    public RoundResult TakeCards(int playerIndex)
    {
        EnsureGameInProgress();
        ValidatePlayerIndex(playerIndex);

        if (_table.Count == 0)
        {
            throw new InvalidOperationException("There are no cards on the table to take.");
        }

        if (playerIndex != CurrentDefender)
        {
            throw new InvalidOperationException("Only the current defender can take the cards.");
        }

        foreach (var played in _table)
        {
            AddToPlayer(playerIndex, played.AttackCard);
            if (played.DefenseCard is not null)
            {
                AddToPlayer(playerIndex, played.DefenseCard);
            }
        }

        return CompleteRound(defenderTookCards: true);
    }

    private RoundResult? TryCompleteSuccessfulRound()
    {
        var allDefended = _table.Count > 0 && _table.All(played => played.DefenseCard is not null);
        var allAttackersPassed = ActiveAttackers().All(index => _passedAttackers.Contains(index));

        return allDefended && allAttackersPassed
            ? CompleteRound(defenderTookCards: false)
            : null;
    }

    private RoundResult CompleteRound(bool defenderTookCards)
    {
        var roundAttacker = _mainAttacker;
        var roundDefender = CurrentDefender;
        var tableSnapshot = _table.ToArray();

        var refillOrder = RefillOrder(roundAttacker, roundDefender);
        foreach (var playerIndex in refillOrder)
        {
            if (IsFinished(playerIndex))
            {
                continue;
            }

            while (_players[playerIndex].CardCount() < _initialHandSize && _deck.Count() > 0)
            {
                AddToPlayer(playerIndex, _deck.DrawCard());
            }
        }

        if (_deck.Count() == 0)
        {
            FinishPlayersWithoutCards(refillOrder);

            var activePlayers = Enumerable.Range(0, PlayerCount)
                .Where(index => !IsFinished(index))
                .ToArray();
            if (activePlayers.Length <= 1)
            {
                _gameOver = true;
                _gameWinner = _finishedPlayers.Count > 0 ? _finishedPlayers[0] : null;
                _gameLoser = activePlayers.Length == 1 ? activePlayers[0] : null;
            }
        }

        _table.Clear();
        _passedAttackers.Clear();

        var nextAttacker = FindNextRoundAttacker(roundDefender, defenderTookCards);
        CurrentAttacker = nextAttacker;
        _mainAttacker = nextAttacker;
        CurrentDefender = NextPlayerWithCards(CurrentAttacker);
        _maxCardsOnTable = Math.Min(MaxCardsOnTable, _players[CurrentDefender].CardCount());

        return new RoundResult
        {
            Attacker = roundAttacker,
            Defender = roundDefender,
            DefenderTookCards = defenderTookCards,
            CardsOnTable = tableSnapshot,
            PlayerCardCounts = CardCounts(),
            NextAttacker = CurrentAttacker,
            FinishedPlayers = FinishedPlayers,
            PlayerPlacements = PlayerPlacements,
            GameOver = _gameOver,
            GameWinner = _gameWinner,
            GameLoser = _gameLoser,
        };
    }

    private int FindStartingPlayer()
    {
        var allCards = Enumerable.Range(0, PlayerCount)
            .SelectMany(index => _players[index].Cards)
            .ToArray();
        var trumps = allCards.Where(card => card.Suit == TrumpSuit).ToArray();
        var openingCard = trumps.Length > 0 ? trumps.Min()! : allCards.Min()!;

        return Enumerable.Range(0, PlayerCount)
            .First(index => _players[index].Cards.Contains(openingCard));
    }

    private int FindNextRoundAttacker(int defender, bool defenderTookCards)
    {
        var start = defenderTookCards ? (defender + 1) % PlayerCount : defender;
        return NextPlayerWithCards(start, includeStart: true);
    }

    private int NextPlayerWithCards(int playerIndex, bool includeStart = false)
    {
        var offset = includeStart ? 0 : 1;
        for (var i = 0; i < PlayerCount; i++)
        {
            var candidate = (playerIndex + offset + i) % PlayerCount;
            if (!IsFinished(candidate) && _players[candidate].CardCount() > 0)
            {
                return candidate;
            }
        }

        return playerIndex;
    }

    private int[] RefillOrder(int attacker, int defender) =>
        [.. Enumerable.Range(0, PlayerCount)
            .Select(offset => (attacker + offset) % PlayerCount)
            .Where(index => index != defender && !IsFinished(index))
            .Append(defender)
            .Where(index => !IsFinished(index))];

    private IEnumerable<int> ActiveAttackers() =>
        Enumerable.Range(0, PlayerCount)
            .Where(index => index != CurrentDefender && !IsFinished(index)
                && _players[index].CardCount() > 0);

    private void FinishPlayersWithoutCards(IEnumerable<int> order)
    {
        foreach (var playerIndex in order)
        {
            if (_players[playerIndex].CardCount() == 0 && !IsFinished(playerIndex))
            {
                _finishedPlayers.Add(playerIndex);
            }
        }
    }

    private bool IsFinished(int playerIndex) => _finishedPlayers.Contains(playerIndex);

    private void RemoveFromPlayer(int playerIndex, Card card)
    {
        if (IsFinished(playerIndex))
        {
            throw new InvalidOperationException("That player has already finished the game.");
        }

        if (!_players[playerIndex].HasCard(card))
        {
            throw new InvalidOperationException("The player does not have that card.");
        }

        _players[playerIndex].RemoveCard(card);
        if (_deck.Count() == 0 && _players[playerIndex].CardCount() == 0)
        {
            _finishedPlayers.Add(playerIndex);
        }
    }

    private void AddToPlayer(int playerIndex, Card card)
    {
        if (IsFinished(playerIndex))
        {
            throw new InvalidOperationException("A finished player cannot receive cards.");
        }

        _players[playerIndex].AddCard(card);
    }

    private bool CanBeat(Card attackCard, Card defenseCard)
    {
        if (attackCard.Suit == defenseCard.Suit)
        {
            return defenseCard.Value > attackCard.Value;
        }

        return defenseCard.Suit == TrumpSuit && attackCard.Suit != TrumpSuit;
    }

    private void EnsureCanAddAttack(int playerIndex, bool defenderMayAct = false, bool checkPasses = true)
    {
        if (IsFinished(playerIndex))
        {
            throw new InvalidOperationException("That player has already finished the game.");
        }

        if (playerIndex == CurrentDefender && !defenderMayAct)
        {
            throw new InvalidOperationException("The defender cannot add an attack card.");
        }

        if (_players[playerIndex].CardCount() == 0)
        {
            throw new InvalidOperationException("A player without cards cannot add to the attack.");
        }

        if (checkPasses && (_passedAttackers.Contains(playerIndex)
            || ActiveAttackers().All(index => _passedAttackers.Contains(index))))
        {
            throw new InvalidOperationException("The attacking players have already passed.");
        }
    }

    private void ValidatePlayerIndex(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= PlayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }
    }

    private void EnsureGameInProgress()
    {
        if (_gameOver)
        {
            throw new InvalidOperationException("The game is over.");
        }
    }
}
