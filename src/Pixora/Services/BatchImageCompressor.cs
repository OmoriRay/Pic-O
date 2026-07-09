using Pixora.Models;
using System.IO;

namespace Pixora.Services;

public sealed record BatchCompressionOptions(
    string InputPath,
    string OutputFolder,
    ImageCompressionFormat Format,
    int JpegQuality,
    int MaxWidth,
    int MaxHeight,
    bool IncludeSubfolders,
    bool OverwriteExisting);

public sealed record BatchCompressionProgress(
    int Completed,
    int Total,
    string CurrentFile,
    string Message);

public sealed record BatchCompressionIssue(
    string FilePath,
    string Reason);

public sealed record BatchCompressionPreflightResult(
    int Total,
    int Compressible,
    int Animated,
    int Failed,
    IReadOnlyList<BatchCompressionIssue> AnimatedItems,
    IReadOnlyList<BatchCompressionIssue> FailedItems);

public sealed record BatchCompressionResult(
    int Total,
    int Saved,
    int Skipped,
    int Failed,
    long OriginalBytes,
    long OutputBytes,
    IReadOnlyList<BatchCompressionIssue> SkippedItems,
    IReadOnlyList<BatchCompressionIssue> FailedItems);

public static class BatchImageCompressor
{
    private const int PreflightProgressInterval = 25;

    public static Task<BatchCompressionPreflightResult> PreflightAsync(
        BatchCompressionOptions options,
        IProgress<BatchCompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => PreflightCore(options, progress, cancellationToken), cancellationToken);
    }

    public static Task<BatchCompressionResult> CompressAsync(
        BatchCompressionOptions options,
        IProgress<BatchCompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => CompressCore(options, progress, cancellationToken), cancellationToken);
    }

    public static string GetDefaultOutputFolder(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(inputPath);
            if (Directory.Exists(fullPath))
            {
                return Path.Combine(fullPath, "compressed");
            }

            if (File.Exists(fullPath))
            {
                var folder = Path.GetDirectoryName(fullPath);
                return string.IsNullOrWhiteSpace(folder)
                    ? string.Empty
                    : Path.Combine(folder, "compressed");
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public static IReadOnlyList<string> FindInputFiles(BatchCompressionOptions options)
    {
        var inputPath = Path.GetFullPath(options.InputPath);
        var outputFolder = string.IsNullOrWhiteSpace(options.OutputFolder)
            ? string.Empty
            : Path.GetFullPath(options.OutputFolder);
        var skipOutputSubtree = Directory.Exists(inputPath)
            && !string.IsNullOrWhiteSpace(outputFolder)
            && IsStrictlyUnderDirectory(outputFolder, inputPath);

        if (File.Exists(inputPath))
        {
            return ImageCatalog.IsSupportedStillImagePath(inputPath) ? [inputPath] : [];
        }

        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException($"输入路径不存在：{inputPath}");
        }

        var files = new List<string>();
        foreach (var file in EnumerateFilesSafe(inputPath, options.IncludeSubfolders))
        {
            if (!ImageCatalog.IsSupportedStillImagePath(file))
            {
                continue;
            }

            if (skipOutputSubtree && IsUnderDirectory(file, outputFolder))
            {
                continue;
            }

            files.Add(file);
        }

        return files
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsOutputFolderInsideInputFolder(BatchCompressionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath) || string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            return false;
        }

        try
        {
            var inputPath = Path.GetFullPath(options.InputPath);
            if (!Directory.Exists(inputPath))
            {
                return false;
            }

            var outputFolder = Path.GetFullPath(options.OutputFolder);
            return IsUnderDirectory(outputFolder, inputPath);
        }
        catch
        {
            return false;
        }
    }

    public static (int Width, int Height)? CalculateResizeDimensions(
        int originalWidth,
        int originalHeight,
        int maxWidth,
        int maxHeight)
    {
        if (originalWidth <= 0 || originalHeight <= 0)
        {
            return null;
        }

        if (maxWidth <= 0 && maxHeight <= 0)
        {
            return null;
        }

        var scale = 1.0;
        if (maxWidth > 0)
        {
            scale = Math.Min(scale, maxWidth / (double)originalWidth);
        }

        if (maxHeight > 0)
        {
            scale = Math.Min(scale, maxHeight / (double)originalHeight);
        }

        if (scale >= 0.999999)
        {
            return null;
        }

        return (
            Math.Max(1, (int)Math.Round(originalWidth * scale)),
            Math.Max(1, (int)Math.Round(originalHeight * scale)));
    }

    private static BatchCompressionResult CompressCore(
        BatchCompressionOptions options,
        IProgress<BatchCompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var files = FindInputFiles(options);
        var total = files.Count;
        var saved = 0;
        var skipped = 0;
        var failed = 0;
        var originalBytes = 0L;
        var outputBytes = 0L;
        var skippedItems = new List<BatchCompressionIssue>();
        var failedItems = new List<BatchCompressionIssue>();

        if (total == 0)
        {
            progress?.Report(new BatchCompressionProgress(0, 0, string.Empty, "没有找到可压缩的图片。"));
            return new BatchCompressionResult(0, 0, 0, 0, 0, 0, [], []);
        }

        Directory.CreateDirectory(options.OutputFolder);

        var inputRoot = Directory.Exists(options.InputPath)
            ? Path.GetFullPath(options.InputPath)
            : Path.GetDirectoryName(Path.GetFullPath(options.InputPath));

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[index];
            var completed = index + 1;
            var fileName = Path.GetFileName(file);
            progress?.Report(new BatchCompressionProgress(index, total, file, $"正在处理：{fileName}"));

            try
            {
                var fileInfo = new FileInfo(file);
                originalBytes += fileInfo.Exists ? fileInfo.Length : 0;

                var document = ImageLoader.Load(file, cancellationToken);
                if (document.IsAnimated)
                {
                    const string reason = "动图暂不支持批量压缩";
                    skipped++;
                    skippedItems.Add(new BatchCompressionIssue(file, reason));
                    progress?.Report(new BatchCompressionProgress(completed, total, file, $"跳过：{fileName}，{reason}"));
                    continue;
                }

                var resize = CalculateResizeDimensions(
                    document.PixelWidth,
                    document.PixelHeight,
                    options.MaxWidth,
                    options.MaxHeight);
                var targetPath = GetOutputPath(file, inputRoot, options);
                var compressionOptions = resize is { } size
                    ? new ImageCompressionOptions(options.Format, options.JpegQuality, targetPath, size.Width, size.Height)
                    : new ImageCompressionOptions(options.Format, options.JpegQuality, targetPath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                var result = ImageCompressor.Save(document.Bitmap, compressionOptions);
                saved++;
                outputBytes += result.OutputBytes;
                progress?.Report(new BatchCompressionProgress(completed, total, file, $"完成：{fileName}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var reason = FriendlyException(ex);
                failed++;
                failedItems.Add(new BatchCompressionIssue(file, reason));
                progress?.Report(new BatchCompressionProgress(completed, total, file, $"失败：{fileName}，{reason}"));
            }
        }

        return new BatchCompressionResult(
            total,
            saved,
            skipped,
            failed,
            originalBytes,
            outputBytes,
            skippedItems,
            failedItems);
    }

    private static BatchCompressionPreflightResult PreflightCore(
        BatchCompressionOptions options,
        IProgress<BatchCompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var files = FindInputFiles(options);
        var total = files.Count;
        var compressible = 0;
        var animatedItems = new List<BatchCompressionIssue>();
        var failedItems = new List<BatchCompressionIssue>();

        if (total == 0)
        {
            progress?.Report(new BatchCompressionProgress(0, 0, string.Empty, "预扫描：没有找到可压缩的图片。"));
            return new BatchCompressionPreflightResult(0, 0, 0, 0, [], []);
        }

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[index];
            var completed = index + 1;
            var fileName = Path.GetFileName(file);
            if (completed == 1 || completed == total || completed % PreflightProgressInterval == 0)
            {
                progress?.Report(new BatchCompressionProgress(completed, total, file, $"预扫描：{completed:N0}/{total:N0}"));
            }

            try
            {
                var document = ImageLoader.Load(file, cancellationToken);
                if (document.IsAnimated)
                {
                    const string reason = "动图暂不支持批量压缩";
                    animatedItems.Add(new BatchCompressionIssue(file, reason));
                    progress?.Report(new BatchCompressionProgress(completed, total, file, $"预扫描跳过动图：{fileName}"));
                    continue;
                }

                compressible++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var reason = FriendlyException(ex);
                failedItems.Add(new BatchCompressionIssue(file, reason));
                progress?.Report(new BatchCompressionProgress(completed, total, file, $"预扫描无法读取：{fileName}，{reason}"));
            }
        }

        return new BatchCompressionPreflightResult(
            total,
            compressible,
            animatedItems.Count,
            failedItems.Count,
            animatedItems,
            failedItems);
    }

    private static void ValidateOptions(BatchCompressionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new InvalidDataException("请选择输入文件或目录。");
        }

        if (!File.Exists(options.InputPath) && !Directory.Exists(options.InputPath))
        {
            throw new FileNotFoundException("输入路径不存在。", options.InputPath);
        }

        if (string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            throw new InvalidDataException("请选择输出目录。");
        }

        if (options.JpegQuality is < 1 or > 100)
        {
            throw new InvalidDataException("JPEG 质量必须在 1 到 100 之间。");
        }

        if (options.MaxWidth < 0 || options.MaxHeight < 0)
        {
            throw new InvalidDataException("最大宽度和最大高度不能小于 0。");
        }
    }

    private static string GetOutputPath(string sourcePath, string? inputRoot, BatchCompressionOptions options)
    {
        var relativeFolder = string.Empty;
        if (!string.IsNullOrWhiteSpace(inputRoot) && Directory.Exists(options.InputPath))
        {
            var sourceFolder = Path.GetDirectoryName(sourcePath) ?? inputRoot;
            relativeFolder = Path.GetRelativePath(inputRoot, sourceFolder);
            if (relativeFolder == ".")
            {
                relativeFolder = string.Empty;
            }
        }

        var targetFolder = string.IsNullOrWhiteSpace(relativeFolder)
            ? options.OutputFolder
            : Path.Combine(options.OutputFolder, relativeFolder);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = ImageCompressor.GetExtension(options.Format);
        var targetPath = Path.Combine(targetFolder, $"{baseName}_压缩{extension}");

        return options.OverwriteExisting
            ? targetPath
            : GetAvailablePath(targetPath);
    }

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(folder, $"{name}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{name}_{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
    }

    private static IEnumerable<string> EnumerateFilesSafe(string folder, bool includeSubfolders)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }

        if (!includeSubfolders)
        {
            yield break;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(folder);
        }
        catch
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            foreach (var file in EnumerateFilesSafe(directory, includeSubfolders: true))
            {
                yield return file;
            }
        }
    }

    private static bool IsUnderDirectory(string path, string folder)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, fullFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStrictlyUnderDirectory(string path, string folder)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string FriendlyException(Exception ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
    }
}
