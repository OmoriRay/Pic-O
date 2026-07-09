using System.Windows;

namespace PureView.Services;

public static class ImageViewportMath
{
    public static ImageTransform CalculateFitTransform(
        int imageWidth,
        int imageHeight,
        double viewportWidth,
        double viewportHeight,
        double minScale,
        double maxScale)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return new ImageTransform(1, 0, 0);
        }

        var scale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
        scale = Math.Clamp(scale, minScale, maxScale);

        return CalculateCenteredTransform(imageWidth, imageHeight, viewportWidth, viewportHeight, scale);
    }

    public static ImageTransform CalculateCenteredTransform(
        int imageWidth,
        int imageHeight,
        double viewportWidth,
        double viewportHeight,
        double scale)
    {
        var x = (viewportWidth - imageWidth * scale) / 2.0;
        var y = (viewportHeight - imageHeight * scale) / 2.0;
        return new ImageTransform(scale, x, y);
    }

    public static Rect CalculateTargetRect(int imageWidth, int imageHeight, ImageTransform transform)
    {
        return new Rect(
            transform.OffsetX,
            transform.OffsetY,
            imageWidth * transform.Scale,
            imageHeight * transform.Scale);
    }

    public static Int32Rect? CalculateCropPixelRect(
        Rect viewportSelection,
        int imageWidth,
        int imageHeight,
        ImageTransform transform)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || transform.Scale <= 0 || viewportSelection.IsEmpty)
        {
            return null;
        }

        var selection = Normalize(viewportSelection);
        if (selection.Width <= 0 || selection.Height <= 0)
        {
            return null;
        }

        var left = (selection.Left - transform.OffsetX) / transform.Scale;
        var top = (selection.Top - transform.OffsetY) / transform.Scale;
        var right = (selection.Right - transform.OffsetX) / transform.Scale;
        var bottom = (selection.Bottom - transform.OffsetY) / transform.Scale;

        var clippedLeft = Math.Clamp(left, 0, imageWidth);
        var clippedTop = Math.Clamp(top, 0, imageHeight);
        var clippedRight = Math.Clamp(right, 0, imageWidth);
        var clippedBottom = Math.Clamp(bottom, 0, imageHeight);

        if (clippedRight <= clippedLeft || clippedBottom <= clippedTop)
        {
            return null;
        }

        var x = Math.Clamp((int)Math.Floor(clippedLeft), 0, imageWidth - 1);
        var y = Math.Clamp((int)Math.Floor(clippedTop), 0, imageHeight - 1);
        var rightPixel = Math.Clamp((int)Math.Ceiling(clippedRight), x + 1, imageWidth);
        var bottomPixel = Math.Clamp((int)Math.Ceiling(clippedBottom), y + 1, imageHeight);

        return new Int32Rect(x, y, rightPixel - x, bottomPixel - y);
    }

    private static Rect Normalize(Rect rect)
    {
        return new Rect(
            Math.Min(rect.Left, rect.Right),
            Math.Min(rect.Top, rect.Bottom),
            Math.Abs(rect.Width),
            Math.Abs(rect.Height));
    }
}

public readonly record struct ImageTransform(double Scale, double OffsetX, double OffsetY);
