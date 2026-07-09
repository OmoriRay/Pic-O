using System.IO;
using System.Text.Json;

namespace PureView.Services;

public sealed class BatchCompressionSettings
{
    public string? LastInputPath { get; set; }

    public string? LastOutputFolder { get; set; }

    public ImageCompressionFormat Format { get; set; } = ImageCompressionFormat.Jpeg;

    public int JpegQuality { get; set; } = 82;

    public int MaxWidth { get; set; } = 1920;

    public int MaxHeight { get; set; } = 1920;

    public bool IncludeSubfolders { get; set; }

    public bool OverwriteExisting { get; set; }

    public static BatchCompressionSettings Load()
    {
        return Load(SettingsPath);
    }

    public static BatchCompressionSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new BatchCompressionSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<BatchCompressionSettings>(json) ?? new BatchCompressionSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new BatchCompressionSettings();
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
        if (!Enum.IsDefined(Format))
        {
            Format = ImageCompressionFormat.Jpeg;
        }

        JpegQuality = Math.Clamp(JpegQuality, 1, 100);
        MaxWidth = Math.Max(0, MaxWidth);
        MaxHeight = Math.Max(0, MaxHeight);
        LastInputPath = string.IsNullOrWhiteSpace(LastInputPath) ? null : LastInputPath.Trim();
        LastOutputFolder = string.IsNullOrWhiteSpace(LastOutputFolder) ? null : LastOutputFolder.Trim();
    }

    private static string SettingsPath =>
        Path.Combine(
            AppInfo.LocalDataFolder,
            "batch-compression-settings.json");
}
