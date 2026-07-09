using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PureView.Controls;

public sealed class BitmapViewer : FrameworkElement
{
    private BitmapSource? _source;
    private BitmapScalingMode _scalingMode = BitmapScalingMode.HighQuality;
    private double _viewScale = 1.0;
    private double _offsetX;
    private double _offsetY;

    public BitmapViewer()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    public BitmapSource? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            InvalidateVisual();
        }
    }

    public BitmapScalingMode ScalingMode
    {
        get => _scalingMode;
        set
        {
            if (_scalingMode == value)
            {
                return;
            }

            _scalingMode = value;
            RenderOptions.SetBitmapScalingMode(this, value);
            InvalidateVisual();
        }
    }

    public double ViewScale
    {
        get => _viewScale;
        set
        {
            if (Math.Abs(_viewScale - value) < 0.000001)
            {
                return;
            }

            _viewScale = value;
            InvalidateVisual();
        }
    }

    public double OffsetX
    {
        get => _offsetX;
        set
        {
            if (Math.Abs(_offsetX - value) < 0.000001)
            {
                return;
            }

            _offsetX = value;
            InvalidateVisual();
        }
    }

    public double OffsetY
    {
        get => _offsetY;
        set
        {
            if (Math.Abs(_offsetY - value) < 0.000001)
            {
                return;
            }

            _offsetY = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_source is null || _viewScale <= 0)
        {
            return;
        }

        var target = new Rect(
            _offsetX,
            _offsetY,
            _source.PixelWidth * _viewScale,
            _source.PixelHeight * _viewScale);

        target = SnapToDevicePixels(target);
        drawingContext.DrawImage(_source, target);
    }

    private Rect SnapToDevicePixels(Rect rect)
    {
        var compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (compositionTarget is null)
        {
            return rect;
        }

        var topLeft = compositionTarget.TransformToDevice.Transform(rect.TopLeft);
        var bottomRight = compositionTarget.TransformToDevice.Transform(rect.BottomRight);

        topLeft.X = Math.Round(topLeft.X);
        topLeft.Y = Math.Round(topLeft.Y);
        bottomRight.X = Math.Round(bottomRight.X);
        bottomRight.Y = Math.Round(bottomRight.Y);

        if (bottomRight.X <= topLeft.X)
        {
            bottomRight.X = topLeft.X + 1;
        }

        if (bottomRight.Y <= topLeft.Y)
        {
            bottomRight.Y = topLeft.Y + 1;
        }

        return new Rect(
            compositionTarget.TransformFromDevice.Transform(topLeft),
            compositionTarget.TransformFromDevice.Transform(bottomRight));
    }
}
