using PureView.Models;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PureView.Services;

public static class VideoThumbnailLoader
{
    private const int DocumentThumbnailSize = 1200;
    private const int FallbackWidth = 640;
    private const int FallbackHeight = 360;

    public static ImageDocument LoadDocument(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("视频文件不存在。", path);
        }

        var bitmap = TryLoadShellThumbnail(path, DocumentThumbnailSize, cancellationToken)
            ?? CreateFallbackThumbnail(FallbackWidth, FallbackHeight);

        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return new ImageDocument(
            path,
            bitmap,
            "视频 / 封面预览",
            fileInfo.Length,
            fileInfo.LastWriteTime,
            [],
            isVideo: true);
    }

    public static BitmapSource LoadThumbnail(string path, int size, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bitmap = TryLoadShellThumbnail(path, size, cancellationToken)
            ?? CreateFallbackThumbnail(320, 180);

        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return bitmap;
    }

    private static BitmapSource? TryLoadShellThumbnail(string path, int size, CancellationToken cancellationToken)
    {
        IntPtr bitmapHandle = IntPtr.Zero;
        IShellItemImageFactory? factory = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var factoryId = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, factoryId, out factory);
            factory.GetImage(
                new NativeSize(size, size),
                ShellItemImageFactoryOptions.ThumbnailOnly
                | ShellItemImageFactoryOptions.BiggerSizeOk
                | ShellItemImageFactoryOptions.ScaleUp,
                out bitmapHandle);

            if (bitmapHandle == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }
        catch (Exception ex) when (ex is COMException or FileNotFoundException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (factory is not null)
            {
                Marshal.FinalReleaseComObject(factory);
            }
        }
    }

    private static BitmapSource CreateFallbackThumbnail(int width, int height)
    {
        var pixels = new byte[checked(width * height * 4)];
        var stride = width * 4;

        var centerX = width / 2.0;
        var centerY = height / 2.0;
        var triangleHeight = height * 0.36;
        var triangleWidth = triangleHeight * 0.9;
        var left = centerX - triangleWidth * 0.32;
        var top = centerY - triangleHeight / 2.0;
        var bottom = centerY + triangleHeight / 2.0;
        var right = centerX + triangleWidth * 0.58;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var isPlayTriangle = IsInsidePlayTriangle(x, y, left, right, top, bottom, centerY);
                var edgeShade = IsNearEdge(x, y, width, height) ? 10 : 0;

                pixels[offset + 0] = isPlayTriangle ? (byte)235 : (byte)(22 + edgeShade);
                pixels[offset + 1] = isPlayTriangle ? (byte)244 : (byte)(26 + edgeShade);
                pixels[offset + 2] = isPlayTriangle ? (byte)250 : (byte)(32 + edgeShade);
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);

        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return bitmap;
    }

    private static bool IsInsidePlayTriangle(
        int x,
        int y,
        double left,
        double right,
        double top,
        double bottom,
        double centerY)
    {
        if (y < top || y > bottom || x < left || x > right)
        {
            return false;
        }

        var verticalProgress = y <= centerY
            ? (y - top) / Math.Max(1.0, centerY - top)
            : (bottom - y) / Math.Max(1.0, bottom - centerY);
        var allowedRight = left + (right - left) * Math.Clamp(verticalProgress, 0.0, 1.0);
        return x <= allowedRight;
    }

    private static bool IsNearEdge(int x, int y, int width, int height)
    {
        return x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr bitmap);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(
            NativeSize size,
            ShellItemImageFactoryOptions flags,
            out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Cx = width;
            Cy = height;
        }

        public readonly int Cx;

        public readonly int Cy;
    }

    [Flags]
    private enum ShellItemImageFactoryOptions : uint
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8,
        ScaleUp = 0x100,
    }
}
