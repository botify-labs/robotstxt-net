using System;
using System.Diagnostics;

namespace RobotsTxt
{
    class ParsedRobotsKey
    {
        private byte[]? _keyText;
        const bool AllowFrequentTypos = true;

        public enum KeyType
        {
            // Generic highlevel fields.
            UserAgent,
            Sitemap,

            // Fields within a user-agent.
            Allow,
            Disallow,

            // Unrecognized field; kept as-is. High number so that additions to the
            // enumeration above does not change the serialization.
            Unknown = 128,
        };

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

        private bool KeyIsSitemap(ReadOnlySpan<byte> key)
        {
            return key.StartsWithIgnoreCase("sitemap") ||
                   key.StartsWithIgnoreCase("site-map");
        }

        private bool KeyIsDisallow(ReadOnlySpan<byte> key)
        {
            return (
                key.StartsWithIgnoreCase("disallow") ||
                (AllowFrequentTypos && (key.StartsWithIgnoreCase("dissallow") ||
                                        key.StartsWithIgnoreCase("dissalow") ||
                                        key.StartsWithIgnoreCase("disalow") ||
                                        key.StartsWithIgnoreCase("diasllow") ||
                                        key.StartsWithIgnoreCase("disallaw"))));
        }

        private bool KeyIsAllow(ReadOnlySpan<byte> key)
        {
            return key.StartsWithIgnoreCase("allow");
        }

        private bool KeyIsUserAgent(ReadOnlySpan<byte> key)
        {
            return key.StartsWithIgnoreCase("user-agent") ||
                   (AllowFrequentTypos && (key.StartsWithIgnoreCase("useragent") ||
                                           key.StartsWithIgnoreCase("user agent")));
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
}
