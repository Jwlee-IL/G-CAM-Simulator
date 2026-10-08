using System.Collections;

namespace Gcam.Studio.Services;

/// <summary>Sealed chunks are shared by immutable prefixes; only the unfinished tail is copied at publication.</summary>
internal sealed class ImmutableRecordStore<T>
{
    private const int ChunkSize = 256;
    private readonly List<T[]> _chunks = [];
    private readonly List<T> _tail = new(ChunkSize);
    public int Count { get; private set; }
    public void Add(T item)
    {
        _tail.Add(item); Count++;
        if (_tail.Count == ChunkSize) { _chunks.Add(_tail.ToArray()); _tail.Clear(); }
    }
    public IReadOnlyList<T> Publish() => new Prefix(_chunks.ToArray(), _tail.ToArray(), Count);
    private sealed class Prefix(T[][] chunks, T[] tail, int count) : IReadOnlyList<T>
    {
        public int Count => count;
        public T this[int index] => index < 0 || index >= count ? throw new ArgumentOutOfRangeException(nameof(index))
            : index / ChunkSize < chunks.Length ? chunks[index / ChunkSize][index % ChunkSize] : tail[index % ChunkSize];
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
