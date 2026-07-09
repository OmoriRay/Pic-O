using System.Globalization;

namespace Pixora.Services;

public sealed class NaturalStringComparer : IComparer<string>
{
    private readonly CompareInfo _compareInfo = CultureInfo.CurrentCulture.CompareInfo;

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var ix = 0;
        var iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            var cx = x[ix];
            var cy = y[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                var result = CompareNumberRuns(x, ref ix, y, ref iy);
                if (result != 0)
                {
                    return result;
                }

                continue;
            }

            var startX = ix;
            var startY = iy;

            while (ix < x.Length && !char.IsDigit(x[ix]))
            {
                ix++;
            }

            while (iy < y.Length && !char.IsDigit(y[iy]))
            {
                iy++;
            }

            var textX = x.AsSpan(startX, ix - startX).ToString();
            var textY = y.AsSpan(startY, iy - startY).ToString();
            var textResult = _compareInfo.Compare(
                textX,
                textY,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);

            if (textResult != 0)
            {
                return textResult;
            }
        }

        return x.Length.CompareTo(y.Length);
    }

    private static int CompareNumberRuns(string x, ref int ix, string y, ref int iy)
    {
        var startX = ix;
        var startY = iy;

        while (ix < x.Length && char.IsDigit(x[ix]))
        {
            ix++;
        }

        while (iy < y.Length && char.IsDigit(y[iy]))
        {
            iy++;
        }

        var trimX = startX;
        var trimY = startY;

        while (trimX < ix && x[trimX] == '0')
        {
            trimX++;
        }

        while (trimY < iy && y[trimY] == '0')
        {
            trimY++;
        }

        var lengthX = ix - trimX;
        var lengthY = iy - trimY;

        if (lengthX != lengthY)
        {
            return lengthX.CompareTo(lengthY);
        }

        for (var i = 0; i < lengthX; i++)
        {
            var result = x[trimX + i].CompareTo(y[trimY + i]);
            if (result != 0)
            {
                return result;
            }
        }

        return (ix - startX).CompareTo(iy - startY);
    }
}
