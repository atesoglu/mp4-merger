namespace mp4_merger;

public sealed class NaturalStringComparer : IComparer<string>
{
    public int Compare(string? a, string? b)
    {
        return a is null || b is null ? -1 : SafeNativeMethods.StrCmpLogicalW(a, b);
    }
}