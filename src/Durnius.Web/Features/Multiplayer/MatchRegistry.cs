namespace Durnius.Web.Features.Multiplayer;

public class MatchRegistry
{
    private record Entry(Func<MatchOptions, (IMatch, Response)> Create, int Capacity);

    private readonly Dictionary<string, Entry> _entries = [];

    public MatchRegistry Register<T>(string name) where T : ICreatableMatch<T>
    {
        _entries[name] = new Entry(o => T.Create(o), T.Capacity);
        return this;
    }

    private Entry Get(string name) =>
        _entries.TryGetValue(name, out var entry)
            ? entry
            : throw new ArgumentException($"Unknown match type '{name}'.", nameof(name));

    public (IMatch Match, Response Response) Create(string name, MatchOptions options) =>
        Get(name).Create(options);

    public int GetCapacity(string name) =>
        Get(name).Capacity;

    public List<(string Name, int Capacity)> GetAll() =>
        [.. _entries.Select(kv => (Name: kv.Key, kv.Value.Capacity))];
}
