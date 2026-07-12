using System.IO;
using System.Text.Json;

namespace Pixora.Services;

public enum SavedFileOpenBehavior
{
    None,
    NewWindow,
    CurrentWindow,
}

public enum QuickSearchMode
{
    Index,
    FileName,
}

public sealed class ViewerSettings
{
    public const int DefaultMainImageCacheMegabytes = 768;

    public const int DefaultDisplayPreviewCacheMegabytes = 192;

    public const int DefaultThumbnailDiskCacheMegabytes = 512;

    public bool ShowThumbnailSidebar { get; set; } = true;

    public bool UseDoubleThumbnailColumns { get; set; } = true;

    public QuickSearchMode QuickSearchMode { get; set; } = global::Pixora.Services.QuickSearchMode.Index;

    public bool ShowQuickSearchOnStartup { get; set; }

    public SavedFileOpenBehavior SavedFileOpenBehavior { get; set; } = SavedFileOpenBehavior.None;

    public bool ConfirmDeleteToRecycleBin { get; set; } = true;

    public ImageSortMode SortMode { get; set; } = ImageSortMode.NameNatural;

    public string? LastOpenedFolder { get; set; }

    public bool OpenLastFolderOnStartup { get; set; }

    public bool RememberMainWindowPlacement { get; set; } = true;

    public bool StartMainWindowMaximized { get; set; }

    public bool ReuseExistingWindow { get; set; } = true;

    public bool KeepViewStateWhenNavigating { get; set; }

    public bool WatchFolderChanges { get; set; } = true;

    public double MainWindowWidth { get; set; }

    public double MainWindowHeight { get; set; }

    public double? MainWindowLeft { get; set; }

    public double? MainWindowTop { get; set; }

    public bool MainWindowMaximized { get; set; }

    public bool ShowDirectoryStats { get; set; }

    public bool ShowAnimationControls { get; set; } = true;

    public bool ShowOperationNotifications { get; set; } = true;

    public bool LoadFullResolutionWhenIdle { get; set; }

    public int MainImageCacheMegabytes { get; set; } = DefaultMainImageCacheMegabytes;

    public int DisplayPreviewCacheMegabytes { get; set; } = DefaultDisplayPreviewCacheMegabytes;

    public bool EnableLowMemoryProtection { get; set; } = true;

    public bool UseThumbnailDiskCache { get; set; }

    public int ThumbnailDiskCacheMegabytes { get; set; } = DefaultThumbnailDiskCacheMegabytes;

    public bool IncludePrivatePathsInDiagnostics { get; set; }

    public double ShortcutSettingsWindowWidth { get; set; }

    public double ShortcutSettingsWindowHeight { get; set; }

    public double? ShortcutSettingsWindowLeft { get; set; }

    public double? ShortcutSettingsWindowTop { get; set; }

    public bool ShortcutSettingsWindowMaximized { get; set; }

    public static ViewerSettings Load()
    {
        return Load(SettingsPath);
    }

    public static ViewerSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ViewerSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<ViewerSettings>(json) ?? new ViewerSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new ViewerSettings();
        }
    }

    public void Save()
    {
        Save(SettingsPath);
    }

    public void Save(string path)
    {
        Normalize();
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void Normalize()
    {
        if (!Enum.IsDefined<global::Pixora.Services.QuickSearchMode>(QuickSearchMode))
        {
            QuickSearchMode = global::Pixora.Services.QuickSearchMode.Index;
        }

        MainWindowWidth = NormalizeWindowDimension(MainWindowWidth, 640, 10_000);
        MainWindowHeight = NormalizeWindowDimension(MainWindowHeight, 420, 10_000);
    }

    private static double NormalizeWindowDimension(double value, double minimum, double maximum)
    {
        return double.IsFinite(value) && value >= minimum
            ? Math.Min(value, maximum)
            : 0;
    }

    private static string SettingsPath =>
        Path.Combine(
            AppInfo.LocalDataFolder,
            "viewer-settings.json");
}
