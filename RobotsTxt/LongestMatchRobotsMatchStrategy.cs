using System.Runtime.CompilerServices;

namespace RobotsTxt;

/// <summary>
/// A RobotsMatchStrategy defines a strategy for matching individual lines in a
/// robots.txt file. Each Match* method should return a match priority, which is
/// interpreted as:
///
/// match priority &lt; 0:
///    No match.
///
/// match priority == 0:
///    Match, but treat it as if matched an empty pattern.
///
/// match priority &gt; 0:
///    Match.
/// </summary>
internal static class LongestMatchRobotsMatchStrategy
{
    internal static int MatchAllowSlow(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
    {
        return MatchesSlow(path, pattern) ? pattern.Length : -1;
    }

    internal static int MatchDisallowSlow(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
    {
        return MatchesSlow(path, pattern) ? pattern.Length : -1;
    }

    internal static bool MatchesSlow(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
    {
        var pathlen = path.Length;
        var pos = new int[pathlen + 1];
        var numpos = 1;
        var patlen = pattern.Length;
        for (var j = 0; j < patlen; j++)
        {
            var ch = pattern[j];
            if (ch == '$' && j + 1 == patlen)
            {
                return pos[numpos - 1] == pathlen;
            }

            if (ch == '*')
            {
                numpos = pathlen - pos[0] + 1;
                for (var i = 1; i < numpos; i++)
                {
                    pos[i] = pos[i - 1] + 1;
                }
            }
            else
            {
                // Includes '$' when not at end of pattern.
                var newnumpos = 0;
                for (var i = 0; i < numpos; i++)
                {
                    if (pos[i] < pathlen && path[pos[i]] == ch)
                    {
                        pos[newnumpos++] = pos[i] + 1;
                    }
                }

                numpos = newnumpos;
                if (numpos == 0) return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MatchAllowFast(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern, bool haveWildcards)
    {
        return MatchesFast(path, pattern, haveWildcards) ? pattern.Length : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MatchDisallowFast(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern, bool haveWildcards)
    {
        return MatchesFast(path, pattern, haveWildcards) ? pattern.Length : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesFast(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern, bool haveWildcards)
    {
        if (pattern.Length == 0) return true;
        if (path.Length == 0) return pattern.Length == 0;

        if (!haveWildcards)
        {
            return path.IndexOf(pattern) != -1;
        }

        Span<int> pos = stackalloc int[path.Length + 1];
        var numpos = 1;

        for (var j = 0; j < pattern.Length; j++)
        {
            var ch = pattern[j];

            // Check for end anchor
            if (ch == '$' && j + 1 == pattern.Length)
            {
                return pos[numpos - 1] == path.Length;
            }

            if (ch == '*')
            {
                var startPos = pos[0];
                numpos = path.Length - startPos + 1;

                for (var i = 0; i < numpos; i++)
                {
                    pos[i] = startPos + i;
                }
            }
            else
            {
                var newnumpos = 0;
                var pathLen = path.Length;

                for (var i = 0; i < numpos && pos[i] < pathLen; i++)
                {
                    if (path[pos[i]] == ch)
                    {
                        pos[newnumpos++] = pos[i] + 1;
                    }
                }

                if (newnumpos == 0) return false;
                numpos = newnumpos;
            }
        }

        return true;
    }
}
