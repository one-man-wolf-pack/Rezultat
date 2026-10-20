using System.Collections;

namespace Rezultat.Tests;

internal sealed class TrackedSequence<T>(params T[] items) : IEnumerable<T>
{
    private readonly T[] _items = items;

    public int Enumerations { get; private set; }

    public int MoveNextCalls { get; private set; }

    public int CurrentReads { get; private set; }

    public int Disposals { get; private set; }

    public int PoisonFrom { get; init; } = int.MaxValue;

    public Exception? DisposeException { get; init; }

    public IEnumerator<T> GetEnumerator()
    {
        Enumerations++;
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Enumerator(TrackedSequence<T> owner) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current
        {
            get
            {
                owner.CurrentReads++;
                return owner._items[_index];
            }
        }

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            owner.MoveNextCalls++;
            if (_index + 1 >= owner.PoisonFrom)
            {
                throw new ProbeException($"poisoned element {_index + 1}");
            }

            return ++_index < owner._items.Length;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            owner.Disposals++;
            if (owner.DisposeException is not null)
            {
                throw owner.DisposeException;
            }
        }
    }
}
