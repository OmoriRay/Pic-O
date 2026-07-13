using System.Collections;

namespace Pixora.Services;

public sealed class LazyIndexedList<T> : IReadOnlyList<T>
    where T : class
{
    private readonly Func<int, T> _factory;
    private readonly int _maxCachedItems;
    private readonly Func<T, bool>? _canEvict;
    private readonly Action<T>? _onEvicted;
    private readonly T?[] _items;
    private readonly LinkedList<int> _recentIndices = [];
    private readonly LinkedListNode<int>?[] _recentNodes;

    public LazyIndexedList(
        int count,
        Func<int, T> factory,
        int maxCachedItems = int.MaxValue,
        Func<T, bool>? canEvict = null,
        Action<T>? onEvicted = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _maxCachedItems = Math.Max(1, maxCachedItems);
        _canEvict = canEvict;
        _onEvicted = onEvicted;
        _items = new T?[count];
        _recentNodes = new LinkedListNode<int>?[count];
    }

    public static LazyIndexedList<T> Empty { get; } = new(0, static _ => throw new ArgumentOutOfRangeException());

    public int Count => _items.Length;

    public int CreatedCount { get; private set; }

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Length);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            var item = _items[index];
            if (item is not null)
            {
                Touch(index);
                return item;
            }

            item = _factory(index);
            _items[index] = item;
            CreatedCount++;
            _recentNodes[index] = _recentIndices.AddLast(index);
            TrimCache();
            return item;
        }
    }

    public bool TryGetCreated(int index, out T? item)
    {
        if ((uint)index >= (uint)_items.Length)
        {
            item = null;
            return false;
        }

        item = _items[index];
        return item is not null;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Touch(int index)
    {
        var node = _recentNodes[index];
        if (node is null || ReferenceEquals(node, _recentIndices.Last))
        {
            return;
        }

        _recentIndices.Remove(node);
        _recentIndices.AddLast(node);
    }

    private void TrimCache()
    {
        var checkedCount = 0;
        while (CreatedCount > _maxCachedItems
            && _recentIndices.First is { } node
            && checkedCount < _recentIndices.Count)
        {
            var index = node.Value;
            _recentIndices.RemoveFirst();
            _recentNodes[index] = null;
            var item = _items[index];
            if (item is null)
            {
                continue;
            }

            if (_canEvict is not null && !_canEvict(item))
            {
                _recentNodes[index] = _recentIndices.AddLast(index);
                checkedCount++;
                continue;
            }

            _items[index] = null;
            CreatedCount--;
            _onEvicted?.Invoke(item);
        }
    }
}

public sealed class IndexedProjectionList<T>(IReadOnlyList<T> source, IReadOnlyList<int> sourceIndices) : IReadOnlyList<T>
{
    public int Count => sourceIndices.Count;

    public T this[int index] => source[sourceIndices[index]];

    public int IndexOfSourceIndex(int sourceIndex)
    {
        for (var index = 0; index < sourceIndices.Count; index++)
        {
            if (sourceIndices[index] == sourceIndex)
            {
                return index;
            }
        }

        return -1;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class VirtualizedRow<T>(int index, IReadOnlyList<T> items)
{
    public int Index { get; } = index;

    public IReadOnlyList<T> Items { get; } = items;
}

public sealed class VirtualizedRowCollection<T> : IList
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _columns;
    private readonly int _maxCachedRows;
    private readonly Dictionary<int, VirtualizedRow<T>> _createdRows = [];
    private readonly Queue<int> _createdRowOrder = [];

    public VirtualizedRowCollection(IReadOnlyList<T> source, int columns, int maxCachedRows = 512)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _columns = Math.Max(1, columns);
        _maxCachedRows = Math.Max(1, maxCachedRows);
    }

    public static VirtualizedRowCollection<T> Empty { get; } = new(Array.Empty<T>(), 1);

    public int Count => (_source.Count + _columns - 1) / _columns;

    public int CreatedRowCount => _createdRows.Count;

    public object? this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            if (_createdRows.TryGetValue(index, out var row))
            {
                return row;
            }

            var start = index * _columns;
            var count = Math.Min(_columns, _source.Count - start);
            var items = new T[count];
            for (var offset = 0; offset < count; offset++)
            {
                items[offset] = _source[start + offset];
            }

            row = new VirtualizedRow<T>(index, items);
            _createdRows[index] = row;
            _createdRowOrder.Enqueue(index);
            while (_createdRows.Count > _maxCachedRows && _createdRowOrder.Count > 0)
            {
                _createdRows.Remove(_createdRowOrder.Dequeue());
            }

            return row;
        }
        set => throw new NotSupportedException();
    }

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public int Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(object? value) => IndexOf(value) >= 0;

    public int IndexOf(object? value)
    {
        return value is VirtualizedRow<T> row && row.Index >= 0 && row.Index < Count
            ? row.Index
            : -1;
    }

    public void Insert(int index, object? value) => throw new NotSupportedException();

    public void Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    public void CopyTo(Array array, int index)
    {
        for (var rowIndex = 0; rowIndex < Count; rowIndex++)
        {
            array.SetValue(this[rowIndex], index + rowIndex);
        }
    }

    public IEnumerator GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }
}
