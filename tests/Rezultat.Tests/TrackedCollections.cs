using System.Collections;

namespace Rezultat.Tests;

internal sealed class ListAccess {
    public int Counts { get; set; }

    public List<int> Reads { get; } = [];

    public int Enumerations { get; set; }
}

internal sealed class TrackedList<T>(T[] listItems, T[] readOnlyItems) : IList<T>, IReadOnlyList<T> {
    public TrackedList(params T[] items)
        : this(items, items) {
    }

    public ListAccess List { get; } = new();

    public ListAccess ReadOnly { get; } = new();

    public Exception? IndexerException { get; init; }

    int ICollection<T>.Count => Count(List, listItems);

    int IReadOnlyCollection<T>.Count => Count(ReadOnly, readOnlyItems);

    bool ICollection<T>.IsReadOnly => true;

    T IList<T>.this[int index] {
        get => Read(List, listItems, index);
        set => throw new NotSupportedException();
    }

    T IReadOnlyList<T>.this[int index] => Read(ReadOnly, readOnlyItems, index);

    public IEnumerator<T> GetEnumerator() {
        List.Enumerations++;
        return ((IEnumerable<T>)listItems).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    int IList<T>.IndexOf(T item) => throw new NotSupportedException();

    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    void ICollection<T>.Clear() => throw new NotSupportedException();

    bool ICollection<T>.Contains(T item) => throw new NotSupportedException();

    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();

    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    private static int Count(ListAccess access, T[] items) {
        access.Counts++;
        return items.Length;
    }

    private T Read(ListAccess access, T[] items, int index) {
        access.Reads.Add(index);
        if (IndexerException is not null) {
            throw IndexerException;
        }

        return items[index];
    }
}

internal sealed class ReadOnlyListOnly<T>(TrackedList<T> inner) : IReadOnlyList<T> {
    public int Count => ((IReadOnlyList<T>)inner).Count;

    public T this[int index] => ((IReadOnlyList<T>)inner)[index];

    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class TrackedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> {
    private readonly List<KeyValuePair<TKey, TValue>> _entries;
    private readonly List<KeyValuePair<TKey, TValue>> _readOnlyEntries;

    public TrackedDictionary(params KeyValuePair<TKey, TValue>[] entries)
        : this(entries, entries) {
    }

    public TrackedDictionary(KeyValuePair<TKey, TValue>[] entries, KeyValuePair<TKey, TValue>[] readOnlyEntries) {
        _entries = [.. entries];
        _readOnlyEntries = [.. readOnlyEntries];
    }

    public int DictionaryLookups { get; private set; }

    public int ReadOnlyLookups { get; private set; }

    public List<TKey> LookedUpKeys { get; } = [];

    public TValue MissValue { get; init; } = default!;

    public Exception? LookupException { get; init; }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => throw Forbidden();

    ICollection<TValue> IDictionary<TKey, TValue>.Values => throw Forbidden();

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => throw Forbidden();

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => throw Forbidden();

    int ICollection<KeyValuePair<TKey, TValue>>.Count => throw Forbidden();

    int IReadOnlyCollection<KeyValuePair<TKey, TValue>>.Count => throw Forbidden();

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => throw Forbidden();

    TValue IDictionary<TKey, TValue>.this[TKey key] {
        get => throw Forbidden();
        set => throw Forbidden();
    }

    TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => throw Forbidden();

    bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) {
        DictionaryLookups++;
        return Lookup(_entries, key, out value);
    }

    bool IReadOnlyDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) {
        ReadOnlyLookups++;
        return Lookup(_readOnlyEntries, key, out value);
    }

    bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => throw Forbidden();

    bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => throw Forbidden();

    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw Forbidden();

    bool IDictionary<TKey, TValue>.Remove(TKey key) => throw Forbidden();

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw Forbidden();

    void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw Forbidden();

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => throw Forbidden();

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw Forbidden();

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw Forbidden();

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => throw Forbidden();

    IEnumerator IEnumerable.GetEnumerator() => throw Forbidden();

    private static ProbeException Forbidden() => new("forbidden dictionary member");

    private bool Lookup(List<KeyValuePair<TKey, TValue>> entries, TKey key, out TValue value) {
        LookedUpKeys.Add(key);
        if (LookupException is not null) {
            throw LookupException;
        }

        foreach (var entry in entries) {
            if (EqualityComparer<TKey>.Default.Equals(entry.Key, key)) {
                value = entry.Value;
                return true;
            }
        }

        value = MissValue;
        return false;
    }
}
