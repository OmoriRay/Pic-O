using System.Runtime.InteropServices;

namespace PureView.Services;

public sealed class WindowsLogicalStringComparer : IComparer<string>
{
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

        try
        {
            return Normalize(StrCmpLogicalW(x, y));
        }
        catch (DllNotFoundException)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
        }
        catch (EntryPointNotFoundException)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
        }
    }

    private static int Normalize(int result)
    {
        return result < 0 ? -1 : result > 0 ? 1 : 0;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);
}
