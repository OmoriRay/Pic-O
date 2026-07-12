namespace Pixora.Services;

public static class MediaCatalogLoader
{
    public static Task<ImageCatalog> LoadFolderAsync(
        string folder,
        ImageSortMode sortMode,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        return Task.Run(() =>
        {
            var catalog = new ImageCatalog
            {
                SortMode = sortMode,
            };
            catalog.LoadFromFolder(folder, cancellationToken, progress);
            return catalog;
        }, cancellationToken);
    }

    public static Task<ImageCatalog> LoadFromFileAsync(
        string path,
        ImageSortMode sortMode,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        return Task.Run(() =>
        {
            var catalog = new ImageCatalog
            {
                SortMode = sortMode,
            };
            catalog.LoadFromFile(path, cancellationToken, progress);
            return catalog;
        }, cancellationToken);
    }
}
