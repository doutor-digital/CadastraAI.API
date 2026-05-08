using System.Collections.Concurrent;

namespace CadastraAI.API.Admin;

public interface IRequestLogStore
{
    void Add(RequestLogEntry entry);
    IReadOnlyList<RequestLogEntry> Recent(int max, string? methodFilter, string? pathFilter, int? minStatus);
    void Clear();
}

public sealed class InMemoryRequestLogStore : IRequestLogStore
{
    private const int Capacity = 1000;
    private readonly ConcurrentQueue<RequestLogEntry> _entries = new();

    public void Add(RequestLogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > Capacity && _entries.TryDequeue(out _)) { }
    }

    public IReadOnlyList<RequestLogEntry> Recent(int max, string? methodFilter, string? pathFilter, int? minStatus)
    {
        var snap = _entries.ToArray();
        IEnumerable<RequestLogEntry> seq = snap;

        if (!string.IsNullOrWhiteSpace(methodFilter))
            seq = seq.Where(e => string.Equals(e.Method, methodFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(pathFilter))
            seq = seq.Where(e => e.Path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase));

        if (minStatus is int min)
            seq = seq.Where(e => e.Status >= min);

        var clamped = Math.Clamp(max, 1, Capacity);
        return seq.Reverse().Take(clamped).ToList();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
