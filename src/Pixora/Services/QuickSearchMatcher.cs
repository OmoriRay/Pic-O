using System.Globalization;
using System.IO;

namespace Pixora.Services;

public static class QuickSearchMatcher
{
    public static bool TryResolveOneBasedIndex(string query, int count, out int zeroBasedIndex)
    {
        zeroBasedIndex = -1;
        if (!int.TryParse(query.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var oneBasedIndex)
            || oneBasedIndex < 1
            || oneBasedIndex > count)
        {
            return false;
        }

        zeroBasedIndex = oneBasedIndex - 1;
        return true;
    }

    public static int FindFileNameMatch(IReadOnlyList<string> paths, string query)
    {
        var keyword = query.Trim();
        if (keyword.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < paths.Count; index++)
        {
            if (MatchesFileName(paths[index], keyword))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool MatchesFileName(string path, string query)
    {
        var keyword = query.Trim();
        return keyword.Length > 0
            && Path.GetFileName(path).Contains(keyword, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string CompactFileName(string path, int leadingCharacters = 8, int trailingCharacters = 8)
    {
        var fileName = Path.GetFileName(path);
        if (leadingCharacters < 1
            || trailingCharacters < 1
            || fileName.Length <= leadingCharacters + trailingCharacters + 1)
        {
            return fileName;
        }

        return $"{fileName[..leadingCharacters]}…{fileName[^trailingCharacters..]}";
    }
}
