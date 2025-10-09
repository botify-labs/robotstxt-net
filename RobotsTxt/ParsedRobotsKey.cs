using System.Diagnostics;

namespace RobotsTxt;

internal class ParsedRobotsKey
{
    private byte[]? _keyText;
    private const bool AllowFrequentTypos = true;

    public enum KeyType
    {
        // Generic high level fields.
        UserAgent,
        Sitemap,

        // Fields within a user-agent.
        Allow,
        Disallow,

        // Unrecognized field; kept as-is. High number so that additions to the
        // enumeration above does not change the serialization.
        Unknown = 128,
    }

    public void Parse(ReadOnlySpan<byte> key)
    {
        _keyText = null;
        if (KeyIsUserAgent(key))
        {
            Type = KeyType.UserAgent;
        }
        else if (KeyIsAllow(key))
        {
            Type = KeyType.Allow;
        }
        else if (KeyIsDisallow(key))
        {
            Type = KeyType.Disallow;
        }
        else if (KeyIsSitemap(key))
        {
            Type = KeyType.Sitemap;
        }
        else
        {
            Type = KeyType.Unknown;
            UnknownText = key.ToArray();
        }
    }

    private static bool KeyIsSitemap(ReadOnlySpan<byte> key)
    {
        return key.StartsWithIgnoreCase("sitemap"u8) ||
               key.StartsWithIgnoreCase("site-map"u8);
    }

    private static bool KeyIsDisallow(ReadOnlySpan<byte> key)
    {
        return key.StartsWithIgnoreCase("disallow"u8) ||
               (AllowFrequentTypos && (key.StartsWithIgnoreCase("dissallow"u8) ||
                                       key.StartsWithIgnoreCase("dissalow"u8) ||
                                       key.StartsWithIgnoreCase("disalow"u8) ||
                                       key.StartsWithIgnoreCase("diasllow"u8) ||
                                       key.StartsWithIgnoreCase("disallaw"u8)));
    }

    private static bool KeyIsAllow(ReadOnlySpan<byte> key)
    {
        return key.StartsWithIgnoreCase("allow"u8);
    }

    private static bool KeyIsUserAgent(ReadOnlySpan<byte> key)
    {
        return key.StartsWithIgnoreCase("user-agent"u8) ||
               (AllowFrequentTypos && (key.StartsWithIgnoreCase("useragent"u8) ||
                                       key.StartsWithIgnoreCase("user agent"u8)));
    }


    public KeyType Type { get; private set; } = KeyType.Unknown;

    public byte[]? UnknownText
    {
        get
        {
            Debug.Assert(Type == KeyType.Unknown);
            return _keyText;
        }
        private set => _keyText = value;
    }
}
