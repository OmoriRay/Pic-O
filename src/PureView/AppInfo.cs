using System.IO;

namespace PureView;

public static class AppInfo
{
    public const string Name = "Pic-O";
    public const string DataFolderName = "Pic-O";
    public const string PreviousDataFolderName = "PureView";
    public const string FileAssociationProgId = "PicO.Image";
    public const string FileTypeDisplayName = "Pic-O 图片";
    public const string Description = "Pic-O 图片查看器";
    public const string FileTypeIconRelativePath = @"Assets\PicOImage.ico";
    public const string WallpaperFilePrefix = "Pic-O-wallpaper-";
    public const string BatchCompressLogPrefix = "Pic-O_batch-compress_";
    public const string CapabilitiesPath = "Software\\" + Name + "\\Capabilities";

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

    public static string PreviousLocalDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PreviousDataFolderName);

    public static void EnsureLocalDataMigrated()
    {
        try
        {
            if (string.Equals(LocalDataFolder, PreviousLocalDataFolder, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(PreviousLocalDataFolder))
            {
                return;
            }

            Directory.CreateDirectory(LocalDataFolder);
            foreach (var fileName in MigratedDataFiles)
            {
                var source = Path.Combine(PreviousLocalDataFolder, fileName);
                var destination = Path.Combine(LocalDataFolder, fileName);
                if (File.Exists(source) && !File.Exists(destination))
                {
                    File.Copy(source, destination);
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
