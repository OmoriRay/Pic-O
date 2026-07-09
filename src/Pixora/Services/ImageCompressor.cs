using ImageMagick;
using ImageMagick.Formats;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pixora.Services;

public enum ImageCompressionFormat
{
    Jpeg,
    Png,
}

public sealed record ImageCompressionOptions(
    ImageCompressionFormat Format,
    int JpegQuality,
    string OutputPath,
    int? ResizeWidth = null,
    int? ResizeHeight = null);

public sealed record ImageCompressionResult(
    string OutputPath,
    long OutputBytes);

public sealed record ImageCompressionEstimate(
    int PixelWidth,
    int PixelHeight,
    long EstimatedBytes);

public static class ImageCompressor
{
    private const long MaxOutputPixelCount = 120_000_000;
    private const long MaxInputPixelCount = 160_000_000;

    public static ImageCompressionResult Save(BitmapSource source, ImageCompressionOptions options)
    {
        ValidateSource(source);
        using var image = PrepareMagickImage(source, options);

        using (var stream = new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            Write(image, options, stream);
        }

        return new ImageCompressionResult(options.OutputPath, new FileInfo(options.OutputPath).Length);
    }

    public static ImageCompressionEstimate Estimate(BitmapSource source, ImageCompressionOptions options)
    {
        ValidateSource(source);
        using var image = PrepareMagickImage(source, options);

        using var stream = new MemoryStream();
        Write(image, options, stream);
        return new ImageCompressionEstimate((int)image.Width, (int)image.Height, stream.Length);
    }

    public static string GetDefaultOutputPath(string sourcePath, ImageCompressionFormat format)
    {
        var folder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return Path.Combine(folder, GetDefaultOutputFileName(sourcePath, format));
    }

    public static string GetDefaultOutputFileName(string sourcePath, ImageCompressionFormat format)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "compressed";
        }

        return $"{name}_压缩{GetExtension(format)}";
    }

    public static string EnsureExtension(string outputPath, ImageCompressionFormat format)
    {
        var extension = Path.GetExtension(outputPath);
        if (format == ImageCompressionFormat.Jpeg
            && (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            return outputPath;
        }

        if (format == ImageCompressionFormat.Png
            && extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return outputPath;
        }

        return Path.ChangeExtension(outputPath, GetExtension(format));
    }

    public static string GetExtension(ImageCompressionFormat format)
    {
        return format == ImageCompressionFormat.Jpeg ? ".jpg" : ".png";
    }

    private static void ValidateSource(BitmapSource source)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new InvalidDataException("图片尺寸无效。");
        }

        var inputPixels = (long)source.PixelWidth * source.PixelHeight;
        if (inputPixels > MaxInputPixelCount)
        {
            throw new InvalidDataException($"图片尺寸过大，已超过保护阈值 {MaxInputPixelCount:N0} 像素。");
        }
    }

    private static MagickImage PrepareMagickImage(BitmapSource source, ImageCompressionOptions options)
    {
        var image = CreateMagickImage(source);

        try
        {
            ResizeIfNeeded(image, options);

            if (options.Format == ImageCompressionFormat.Jpeg)
            {
                PrepareForJpeg(image, options.JpegQuality);
            }
            else if (options.Format == ImageCompressionFormat.Png)
            {
                PrepareForPng(image);
            }
            else
            {
                throw new NotSupportedException("不支持该压缩格式。");
            }

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static MagickImage CreateMagickImage(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);

        var settings = new PixelReadSettings(
            (uint)converted.PixelWidth,
            (uint)converted.PixelHeight,
            StorageType.Char,
            PixelMapping.BGRA);

        var image = new MagickImage(pixels, settings)
        {
            Density = new Density(source.DpiX, source.DpiY),
        };
        return image;
    }

    private static void ResizeIfNeeded(MagickImage image, ImageCompressionOptions options)
    {
        var targetWidth = options.ResizeWidth ?? (int)image.Width;
        var targetHeight = options.ResizeHeight ?? (int)image.Height;
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new InvalidDataException("输出分辨率无效。");
        }

        var targetPixels = (long)targetWidth * targetHeight;
        if (targetPixels > MaxOutputPixelCount)
        {
            throw new InvalidDataException($"输出尺寸过大，已超过保护阈值 {MaxOutputPixelCount:N0} 像素。");
        }

        if (targetWidth == image.Width && targetHeight == image.Height)
        {
            return;
        }

        var geometry = new MagickGeometry((uint)targetWidth, (uint)targetHeight)
        {
            IgnoreAspectRatio = true,
        };
        image.Resize(geometry, FilterType.Lanczos);
    }

    private static void PrepareForJpeg(MagickImage image, int jpegQuality)
    {
        if (image.HasAlpha)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        image.Strip();
        image.ColorSpace = ColorSpace.sRGB;
        image.Depth = 8;
        image.Quality = (uint)Math.Clamp(jpegQuality, 1, 100);
        image.Settings.Interlace = Interlace.Plane;
        image.Format = MagickFormat.Jpeg;
    }

    private static void PrepareForPng(MagickImage image)
    {
        image.Strip();
        image.Depth = 8;
        image.Format = image.HasAlpha ? MagickFormat.Png32 : MagickFormat.Png24;
    }

    private static void Write(MagickImage image, ImageCompressionOptions options, Stream output)
    {
        if (options.Format == ImageCompressionFormat.Jpeg)
        {
            image.Write(output, CreateJpegWriteDefines(options.JpegQuality));
            return;
        }

        if (options.Format == ImageCompressionFormat.Png)
        {
            image.Write(output, CreatePngWriteDefines());
            return;
        }

        throw new NotSupportedException("不支持该压缩格式。");
    }

    private static JpegWriteDefines CreateJpegWriteDefines(int jpegQuality)
    {
        var quality = Math.Clamp(jpegQuality, 1, 100);
        return new JpegWriteDefines
        {
            DctMethod = JpegDctMethod.Float,
            OptimizeCoding = true,
            SamplingFactor = SelectJpegSamplingFactor(quality),
        };
    }

    private static JpegSamplingFactor SelectJpegSamplingFactor(int quality)
    {
        if (quality >= 86)
        {
            return JpegSamplingFactor.Ratio444;
        }

        if (quality >= 68)
        {
            return JpegSamplingFactor.Ratio422;
        }

        return JpegSamplingFactor.Ratio420;
    }

    private static PngWriteDefines CreatePngWriteDefines()
    {
        return new PngWriteDefines
        {
            CompressionLevel = 9,
            CompressionStrategy = PngCompressionStrategy.Adaptive,
        };
    }
}
