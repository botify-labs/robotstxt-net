using System;

namespace RobotsTxt
{
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
        internal static int MatchAllow(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
        {
            return Matches(path, pattern) ? pattern.Length : -1;
        }

        internal static int MatchDisallow(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
        {
            return Matches(path, pattern) ? pattern.Length : -1;
        }

        internal static bool Matches(ReadOnlySpan<byte> path, ReadOnlySpan<byte> pattern)
        {
            var pathlen = path.Length;
            var pos = new int[pathlen + 1];
            int numpos = 1;
            var patlen = pattern.Length;
            for (var j = 0; j < patlen; j++)
            {
                var ch = pattern[j];
                if (ch == '$' && j + 1 == patlen)
                {
                    return (pos[numpos - 1] == pathlen);
                }

                if (ch == '*')
                {
                    numpos = pathlen - pos[0] + 1;
                    for (int i = 1; i < numpos; i++)
                    {
                        pos[i] = pos[i - 1] + 1;
                    }
                }
                else
                {
                    // Includes '$' when not at end of pattern.
                    int newnumpos = 0;
                    for (int i = 0; i < numpos; i++)
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
    }
}
