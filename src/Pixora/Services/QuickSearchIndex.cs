using System.IO;

namespace Pixora.Services;

public sealed record QuickSearchIndexResult(string Query, IReadOnlyList<int> MatchingIndices)
{
    public int FirstIndex => MatchingIndices.Count > 0 ? MatchingIndices[0] : -1;
}

public sealed class QuickSearchIndex
{
    private Entry[] _entries = [];

    public int Count => _entries.Length;

    public void Reset(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var entries = new Entry[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            entries[index] = new Entry(Path.GetFileName(path));
        }

        _entries = entries;
    }

    public QuickSearchIndexResult Search(
        string query,
        QuickSearchIndexResult? previousResult = null,
        CancellationToken cancellationToken = default)
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
                if ((uint)index < (uint)_entries.Length && _entries[index].Matches(keyword))
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
                if (_entries[index].Matches(keyword))
                {
                    matches.Add(index);
                }
            }
        }

        return new QuickSearchIndexResult(keyword, matches);
    }

    public string? GetFileName(int index)
    {
        return (uint)index < (uint)_entries.Length ? _entries[index].FileName : null;
    }

    private sealed record Entry(string FileName)
    {
        public bool Matches(string query)
        {
            return FileName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
