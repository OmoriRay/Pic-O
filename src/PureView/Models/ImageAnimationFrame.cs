using System.Windows.Media.Imaging;

namespace PureView.Models;

public sealed class ImageAnimationFrame
{
    public ImageAnimationFrame(BitmapSource bitmap, TimeSpan delay)
    {
        Bitmap = bitmap;
        Delay = delay;
    }

    public BitmapSource Bitmap { get; }

    public TimeSpan Delay { get; }
}
