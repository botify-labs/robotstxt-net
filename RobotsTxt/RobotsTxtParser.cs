namespace RobotsTxt
{
    public class RobotsTxtParser(byte[] robotsBody, IRobotsParseHandler handler)
    {
        static readonly byte[] UtfBom = [0xEF, 0xBB, 0xBF];
        static readonly byte[] HexDigits = "0123456789ABCDEF"u8.ToArray();

        public void Parse()
        {
            // Certain browsers limit the URL length to 2083 bytes. In a robots.txt, it's
            // fairly safe to assume any valid line isn't going to be more than many times
            // that max url length of 2KB. We want some padding for
            // UTF-8 encoding/nulls/etc. but a much smaller bound would be okay as well.
            // If so, we can ignore the chars on a line past that.
            const int maxLineLen = 2083 * 8;
            // Allocate a buffer used to process the current line.
            var lineBuffer = new byte[maxLineLen];
            var linePos = 0;
            var lineNum = 0;
            var bomPos = 0;
            bool lastWasCarriageReturn = false;
            handler.HandleRobotsStart();

            foreach (var ch in robotsBody)
            {
                // Google-specific optimization: UTF-8 byte order marks should never
                // appear in a robots.txt file, but they do nevertheless. Skipping
                // possible BOM-prefix in the first bytes of the input.
                if (bomPos < 3 && ch == UtfBom[bomPos++])
                {
                    continue;
                }

                bomPos = 3;
                if (ch != '\n' && ch != '\r')
                {
                    // Non-line-ending char case.
                    // Put in next spot on current line, as long as there's room.
                    if (linePos < maxLineLen)
                    {
                        lineBuffer[linePos++] = ch;
                    }
                }
                else
                {
                    // Line-ending character char case.
                    var span = lineBuffer.AsSpan(0, linePos);
                    // Only emit an empty line if this was not due to the second character
                    // of the DOS line-ending \r\n .
                    bool isCrlfContinuation = span.Length == 0 && lastWasCarriageReturn && ch == '\n';
                    if (!isCrlfContinuation)
                    {
                        ParseAndEmitLine(++lineNum, span);
                    }

                    linePos = 0;
                    lastWasCarriageReturn = ch == '\r';
                }
            }

            var spanLeft = lineBuffer.AsSpan(0, linePos);
            ParseAndEmitLine(++lineNum, spanLeft);
            handler.HandleRobotsEnd();
        }

        void ParseAndEmitLine(int currentLine, ReadOnlySpan<byte> line)
        {
            if (!GetKeyAndValueFrom(out var stringKey, out var value, line))
            {
                return;
            }

            ParsedRobotsKey key = new ParsedRobotsKey();
            key.Parse(stringKey);
            if (NeedEscapeValueForKey(key))
            {
                var escapedValue = MaybeEscapePattern(value);
                EmitKeyValueToHandler(currentLine, key, escapedValue);
            }
            else
            {
                EmitKeyValueToHandler(currentLine, key, value);
            }
        }

        private void EmitKeyValueToHandler(int currentLine, ParsedRobotsKey key, ReadOnlySpan<byte> value)
        {
            switch (key.Type)
            {
                case ParsedRobotsKey.KeyType.UserAgent:
                    handler.HandleUserAgent(currentLine, value);
                    break;
                case ParsedRobotsKey.KeyType.Sitemap:
                    handler.HandleSitemap(currentLine, value);
                    break;
                case ParsedRobotsKey.KeyType.Allow:
                    handler.HandleAllow(currentLine, value);
                    break;
                case ParsedRobotsKey.KeyType.Disallow:
                    handler.HandleDisallow(currentLine, value);
                    break;
                case ParsedRobotsKey.KeyType.Unknown:
                    handler.HandleUnknownAction(currentLine, key.UnknownText, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ReadOnlySpan<byte> MaybeEscapePattern(ReadOnlySpan<byte> src)
        {
            int numToEscape = 0;
            bool needCapitalize = false;
            for (int i = 0; i < src.Length; i++)
            {
                // (a) % escape sequence.
                var c = src[i];
                if (c == '%' && i + 2 < src.Length &&
                    (('a' <= src[i + 1] && src[i + 1] <= 'f') || ('a' <= src[i + 2] && src[i + 2] <= 'f')))
                {
                    needCapitalize = true;
                    i += 2;
                }
                // (b) needs escaping.
                else if (c >= 0x80)
                {
                    numToEscape += 1;
                }
                // (c) Already escaped and escape-characters normalized (eg. %2f -> %2F).
            }

            if (numToEscape == 0 && !needCapitalize)
            {
                return src;
            }

            var dst = new byte[numToEscape * 2 + src.Length];
            var j = 0;
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c == '%' && i + 2 < src.Length && src[i + 1].IsXDigit() && src[i + 2].IsXDigit())
                {
                    dst[j++] = src[i++];
                    dst[j++] = src[i++].ToUpper();
                    dst[j++] = src[i++].ToUpper();
                }
                else if (c >= 0x80)
                {
                    dst[j++] = (byte)'%';
                    dst[j++] = HexDigits[(c >> 4) & 0xf];
                    dst[j++] = HexDigits[c & 0xf];
                }
                else
                {
                    dst[j++] = c;
                }
            }

            return dst;
        }

        private bool NeedEscapeValueForKey(ParsedRobotsKey key)
        {
            switch (key.Type)
            {
                case ParsedRobotsKey.KeyType.UserAgent:
                case ParsedRobotsKey.KeyType.Sitemap:
                    return false;
                default:
                    return true;
            }
        }

        internal static bool GetKeyAndValueFrom(out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value,
            ReadOnlySpan<byte> line)
        {
            var comment = line.IndexOf((byte)'#');
            if (comment != -1)
            {
                line = line.Slice(0, comment);
            }

            line = StripWhitespaceSlowly(line);

            // Rules must match the following pattern:
            //   <key>[ \t]*:[ \t]*<value>
            var sep = line.IndexOf((byte)':');
            if (sep == -1)
            {
                // Google-specific optimization: some people forget the colon, so we need to
                // accept whitespace in its stead.
                sep = line.IndexOfAny((byte)' ', (byte)'\t');
                if (sep != -1)
                {
                    var val = line.Slice(sep + 1);
                    if (val.IndexOfAny((byte)' ', (byte)'\t') != -1)
                    {
                        // We only accept whitespace as a separator if there are exactly two
                        // sequences of non-whitespace characters.  If we get here, there were
                        // more than 2 such sequences since we stripped trailing whitespace
                        // above.
                        key = null;
                        value = null;
                        return false;
                    }
                }
            }

            if (sep == -1)
            {
                key = null;
                value = null;
                return false; // Couldn't find a separator.
            }

            key = line.Slice(0, sep); // Key starts at beginning of line. And stops at the separator.
            key = StripWhitespaceSlowly(key); // Get rid of any trailing whitespace.
            if (key.Length > 0)
            {
                value = line.Slice(sep + 1); // Value starts after the separator.
                value = StripWhitespaceSlowly(value); // Get rid of any leading whitespace.
                return true;
            }

            value = null;
            return false;
        }

        internal static ReadOnlySpan<byte> StripWhitespaceSlowly(ReadOnlySpan<byte> s)
        {
            int start, end;
            for (start = 0; start < s.Length; start++)
            {
                if (s[start] != ' ' && s[start] != '\t')
                    break;
            }

            for (end = s.Length; end > start; end--)
            {
                if (s[end - 1] != ' ' && s[end - 1] != '\t')
                    break;
            }

            return s.Slice(start, end - start);
        }
    }
}
