using System.IO;
using System.Text;
using System.Text.Json;

namespace Pixora.Services;

public static class AtomicJsonFile
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
    };

    public static T? Load<T>(string path, JsonSerializerOptions? options = null)
        where T : class
    {
        return TryLoad(path, options, out T? value)
            ? value
            : TryLoad(GetBackupPath(path), options, out value)
                ? value
                : null;
    }

    public static void Save<T>(string path, T value, JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, options ?? DefaultOptions);
        SaveText(path, json);
    }

    public static string GetBackupPath(string path)
    {
        return path + ".bak";
    }

    private static bool TryLoad<T>(string path, JsonSerializerOptions? options, out T? value)
        where T : class
    {
        value = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            value = JsonSerializer.Deserialize<T>(stream, options);
            return value is not null;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static void SaveText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException("设置文件缺少有效的目录路径。");
        }

        Directory.CreateDirectory(folder);
        var temporaryPath = Path.Combine(
            folder,
            $".{Path.GetFileName(fullPath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
                return;
            }

            var backupPath = GetBackupPath(fullPath);
            try
            {
                File.Replace(temporaryPath, fullPath, backupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithFallback(temporaryPath, fullPath, backupPath);
            }
            catch (IOException)
            {
                ReplaceWithFallback(temporaryPath, fullPath, backupPath);
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }

    private static void ReplaceWithFallback(string temporaryPath, string targetPath, string backupPath)
    {
        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, backupPath, overwrite: true);
        }

        File.Move(temporaryPath, targetPath, overwrite: true);
    }
}
