using System.IO;

namespace Pixora;

public static class AppInfo
{
    public const string Name = "Pixora";
    public const string DataFolderName = "Pixora";
    public const string FileAssociationProgId = "Pixora.Image";
    public const string FileTypeDisplayName = "Pixora 图片";
    public const string Description = "Pixora 图片查看器";
    public const string FileTypeIconRelativePath = @"Assets\PixoraImage.ico";
    public const string WallpaperFilePrefix = "Pixora-wallpaper-";
    public const string BatchCompressLogPrefix = "Pixora_batch-compress_";
    public const string CapabilitiesPath = "Software\\" + Name + "\\Capabilities";

    public static readonly string[] PreviousDataFolderNames =
    [
        "Pic-O",
        "PureView",
    ];

    private static readonly string[] MigratedDataFiles =
    [
        "viewer-settings.json",
        "shortcuts.json",
        "favorites.json",
        "batch-compression-settings.json",
    ];

    public static string LocalDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DataFolderName);

    public static IEnumerable<string> PreviousLocalDataFolders =>
        PreviousDataFolderNames.Select(folderName =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                folderName));

    public static void EnsureLocalDataMigrated()
    {
        try
        {
            foreach (var previousLocalDataFolder in PreviousLocalDataFolders)
            {
                if (string.Equals(LocalDataFolder, previousLocalDataFolder, StringComparison.OrdinalIgnoreCase)
                    || !Directory.Exists(previousLocalDataFolder))
                {
                    continue;
                }

                Directory.CreateDirectory(LocalDataFolder);
                foreach (var fileName in MigratedDataFiles)
                {
                    var source = Path.Combine(previousLocalDataFolder, fileName);
                    var destination = Path.Combine(LocalDataFolder, fileName);
                    if (File.Exists(source) && !File.Exists(destination))
                    {
                        File.Copy(source, destination);
                    }
                }
            }
        }
        catch
        {
            // Migration is best-effort; startup should continue even if old data cannot be copied.
        }
    }

    public static string FormatTitle(string? documentName)
    {
        return string.IsNullOrWhiteSpace(documentName)
            ? Name
            : $"{documentName} - {Name}";
    }
}
