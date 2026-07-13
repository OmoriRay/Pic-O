namespace Pixora.Services;

public sealed class ProgressUpdateBuffer<T> : IProgress<T>
{
    private readonly object _syncRoot = new();
    private readonly List<T> _pending = [];

    public int PendingCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _pending.Count;
            }
        }
    }

    public void Report(T value)
    {
        lock (_syncRoot)
        {
            _pending.Add(value);
        }
    }

    public IReadOnlyList<T> Drain()
    {
        lock (_syncRoot)
        {
            if (_pending.Count == 0)
            {
                return [];
            }

            var batch = _pending.ToArray();
            _pending.Clear();
            return batch;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _pending.Clear();
        }
    }
}
