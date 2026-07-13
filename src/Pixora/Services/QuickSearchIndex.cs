using System.IO;

namespace Pixora.Services;

public sealed record QuickSearchIndexResult(string Query, IReadOnlyList<int> MatchingIndices)
{
    public int FirstIndex => MatchingIndices.Count > 0 ? MatchingIndices[0] : -1;
}

public sealed class QuickSearchIndex
{
    private readonly object _syncRoot = new();
    private IReadOnlyList<string> _paths = [];
    private Entry?[] _entries = [];
    private int _initializedEntryCount;

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Length;
            }
        }
    }

    public int InitializedEntryCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _initializedEntryCount;
            }
        }
    }

    public void Reset(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        lock (_syncRoot)
        {
            ResetCore(paths, cancellationToken);
        }
    }

    public QuickSearchIndexResult ResetAndSearch(
        IReadOnlyList<string> paths,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        lock (_syncRoot)
        {
            ResetCore(paths, cancellationToken);
            return SearchCore(query, previousResult: null, cancellationToken);
        }
    }

    public QuickSearchIndexResult Search(
        string query,
        QuickSearchIndexResult? previousResult = null,
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return SearchCore(query, previousResult, cancellationToken);
        }
    }

    private QuickSearchIndexResult SearchCore(
        string query,
        QuickSearchIndexResult? previousResult,
        CancellationToken cancellationToken)
    {
        var keyword = query.Trim();
        if (keyword.Length == 0)
        {
            return new QuickSearchIndexResult(string.Empty, []);
        }

        var previousMatches = previousResult is not null
            && keyword.StartsWith(previousResult.Query, StringComparison.OrdinalIgnoreCase)
            ? previousResult.MatchingIndices
            : null;
        var matches = new List<int>();

        if (previousMatches is not null)
        {
            foreach (var index in previousMatches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((uint)index < (uint)_entries.Length && GetEntry(index).Matches(keyword))
                {
                    matches.Add(index);
                }
            }
        }
        else
        {
            for (var index = 0; index < _entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (GetEntry(index).Matches(keyword))
                {
                    matches.Add(index);
                }
            }
        }

        return new QuickSearchIndexResult(keyword, matches);
    }

    public string? GetFileName(int index)
    {
        lock (_syncRoot)
        {
            return (uint)index < (uint)_entries.Length ? GetEntry(index).FileName : null;
        }
    }

    private void ResetCore(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _paths = paths;
        _entries = new Entry?[paths.Count];
        _initializedEntryCount = 0;
    }

    private Entry GetEntry(int index)
    {
        var entry = _entries[index];
        if (entry is not null)
        {
            return entry;
        }

        entry = new Entry(Path.GetFileName(_paths[index]));
        _entries[index] = entry;
        _initializedEntryCount++;
        return entry;
    }

    private sealed record Entry(string FileName)
    {
        public bool Matches(string query)
        {
            return FileName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
