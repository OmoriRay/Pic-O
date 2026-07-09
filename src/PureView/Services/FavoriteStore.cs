using System.IO;
using System.Text.Json;

namespace PureView.Services;

public sealed class FavoriteStore
{
    private readonly HashSet<string> _paths;

    public FavoriteStore()
    {
        _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private FavoriteStore(IEnumerable<string> paths)
    {
        _paths = new HashSet<string>(
            paths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _paths.Count;

    public IReadOnlyList<string> Paths => _paths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToList();

    public static FavoriteStore Load()
    {
        return Load(StorePath);
    }

    public static FavoriteStore Load(string path)
    {
        if (!File.Exists(path))
        {
            return new FavoriteStore();
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<FavoriteStoreData>(json);
            return new FavoriteStore(data?.Paths ?? []);
        }
        catch
        {
            return new FavoriteStore();
        }
    }

    public bool IsFavorite(string path)
    {
        return _paths.Contains(Path.GetFullPath(path));
    }

    public bool Add(string path)
    {
        return _paths.Add(Path.GetFullPath(path));
    }

    public bool Remove(string path)
    {
        return _paths.Remove(Path.GetFullPath(path));
    }

    public bool RemoveMissingOrUnsupported()
    {
        var stalePaths = _paths
            .Where(static path => !File.Exists(path) || !ImageCatalog.IsSupportedMediaPath(path))
            .ToList();

        foreach (var path in stalePaths)
        {
            _paths.Remove(path);
        }

        return stalePaths.Count > 0;
    }

    public IReadOnlyList<string> GetExistingMediaPaths()
    {
        return _paths
            .Where(static path => File.Exists(path) && ImageCatalog.IsSupportedMediaPath(path))
            .ToList();
    }

    public void Save()
    {
        Save(StorePath);
    }

    public void Save(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var data = new FavoriteStoreData
        {
            Paths = Paths.ToList(),
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string StorePath =>
        Path.Combine(
            AppInfo.LocalDataFolder,
            "favorites.json");

    private sealed class FavoriteStoreData
    {
        public List<string>? Paths { get; set; }
    }
}
