using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace PureView.Services;

public sealed class ThumbnailDiskCache
{
    public static string DefaultCacheFolder =>
        Path.Combine(
            AppInfo.LocalDataFolder,
            "thumbnail-cache");

    public ThumbnailDiskCache(string? folder = null)
    {
        Folder = string.IsNullOrWhiteSpace(folder) ? DefaultCacheFolder : folder;
    }

    public string Folder { get; }

    public bool TryLoad(string path, int maxWidth, int maxHeight, out BitmapSource? thumbnail)
    {
        thumbnail = null;
        try
        {
            var cachePath = GetCachePath(path, maxWidth, maxHeight);
            if (!File.Exists(cachePath))
            {
                return false;
            }

            using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            thumbnail = bitmap;
            return true;
        }
        catch
        {
            thumbnail = null;
            return false;
        }
    }

    public void Save(string path, int maxWidth, int maxHeight, BitmapSource thumbnail)
    {
        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(Folder);
            var cachePath = GetCachePath(path, maxWidth, maxHeight);
            if (File.Exists(cachePath))
            {
                return;
            }

            tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                encoder.Save(stream);
            }

            File.Move(tempPath, cachePath, overwrite: true);
            tempPath = null;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }
    }

    private string GetCachePath(string path, int maxWidth, int maxHeight)
    {
        var fileInfo = new FileInfo(path);
        var key = string.Join(
            "|",
            Path.GetFullPath(path),
            fileInfo.Length.ToString(CultureInfo.InvariantCulture),
            fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            maxWidth.ToString(CultureInfo.InvariantCulture),
            maxHeight.ToString(CultureInfo.InvariantCulture));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(Folder, $"{hash}.png");
    }
}
