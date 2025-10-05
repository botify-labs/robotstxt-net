namespace RobotsTxt;

public static class MyExtensions
{
    public static bool EqualsIgnoreCase(this ReadOnlySpan<byte> self, ReadOnlySpan<byte> other)
    {
        if (self.Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < self.Length; i++)
        {
            var c1 = self[i];
            var c2 = other[i];
            if ('A' <= c1 && c1 <= 'Z')
                c1 += 32;
            if ('A' <= c2 && c2 <= 'Z')
                c2 += 32;
            if (c1 != c2)
            {
                return false;
            }
        }

        return true;
    }

    public static bool StartsWithIgnoreCase(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (span.Length < value.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c1 = span[i];
            var c2 = value[i];
            if ('A' <= c1 && c1 <= 'Z')
                c1 += 32;
            if ('A' <= c2 && c2 <= 'Z')
                c2 += (byte)' ';
            if (c1 != c2)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsXDigit(this byte c)
    {
        return ('0' <= c && c <= '9') || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
    }

    public static bool IsAlpha(this byte c)
    {
        return ('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z');
    }

    public static bool IsSpace(this byte c)
    {
        return c == ' ' || c == '\t';
    }

    public static byte ToUpper(this byte c)
    {
        return (byte)('a' <= c && c <= 'z' ? c - ' ' : c);
    }
}
