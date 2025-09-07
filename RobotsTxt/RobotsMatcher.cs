using System.Diagnostics;
using System.Text;

namespace RobotsTxt
{
    /// <summary>
    /// Create a RobotsMatcher with the default matching strategy. The default
    /// matching strategy is longest-match as opposed to the former internet draft
    /// that provisioned first-match strategy. Analysis shows that longest-match,
    /// while more restrictive for crawlers, is what webmasters assume when writing
    /// directives. For example, in case of conflicting matches (both Allow and
    /// Disallow), the longest match is the one the user wants. For example, in
    /// case of a robots.txt file that has the following rules
    ///   Allow: /
    ///   Disallow: /cgi-bin
    /// it's pretty obvious what the webmaster wants: they want to allow crawl of
    /// every URI except /cgi-bin. However, according to the expired internet
    /// standard, crawlers should be allowed to crawl everything with such a rule.
    /// </summary>
    public class RobotsMatcher : IRobotsParseHandler
    {
        public void HandleRobotsStart()
        {
            // This is a new robots.txt file, so we need to reset all the instance member
            // variables. We do it in the same order the instance member variables are
            // declared, so it's easier to keep track of which ones we have (or maybe
            // haven't!) done.
            _allow.Clear();
            _disallow.Clear();

            _seenGlobalAgent = false;
            _seenSpecificAgent = false;
            _everSeenSpecificAgent = false;
            _seenSeparator = false;
        }

        public void HandleRobotsEnd()
        {
        }

        internal static ReadOnlySpan<byte> ExtractUserAgent(ReadOnlySpan<byte> userAgent)
        {
            // Allowed characters in user-agent are [a-zA-Z_-].
            var i = 0;
            for (; i < userAgent.Length; i++)
            {
                var c = userAgent[i];
                if (!(c.IsAlpha() || c == '_' || c == '-'))
                {
                    break;
                }
            }

            return userAgent.Slice(0, i);
        }

        public void HandleUserAgent(int lineNum, ReadOnlySpan<byte> userAgent)
        {
            if (_seenSeparator)
            {
                _seenSpecificAgent = _seenGlobalAgent = _seenSeparator = false;
            }

            // Google-specific optimization: a '*' followed by space and more characters
            // in a user-agent record is still regarded a global rule.
            if (userAgent.Length >= 1 && userAgent[0] == '*' && (userAgent.Length == 1 || userAgent[1].IsSpace()))
            {
                _seenGlobalAgent = true;
            }
            else
            {
                userAgent = ExtractUserAgent(userAgent);
                Debug.Assert(_userAgents != null);
                foreach (var ua in _userAgents)
                {
                    if (userAgent.EqualsIgnoreCase(ua))
                    {
                        _everSeenSpecificAgent = _seenSpecificAgent = true;
                        break;
                    }
                }
            }
        }

        readonly byte[] _indexHtmBytes = "/index.htm"u8.ToArray();

        public void HandleAllow(int lineNum, ReadOnlySpan<byte> value)
        {
            if (!SeenAnyAgent)
                return;
            Debug.Assert(_allow != null);
            _seenSeparator = true;
            var priority = LongestMatchRobotsMatchStrategy.MatchAllowSlow(_path, value);
            if (priority >= 0)
            {
                if (_seenSpecificAgent)
                {
                    Debug.Assert(_allow.Specific != null);
                    if (_allow.Specific.Priority < priority)
                    {
                        _allow.Specific.Set(priority);
                    }
                }
                else
                {
                    Debug.Assert(_seenGlobalAgent);
                    Debug.Assert(_allow.Global != null);
                    if (_allow.Global.Priority < priority)
                    {
                        _allow.Global.Set(priority);
                    }
                }
            }
            else
            {
                // Google-specific optimization: 'index.htm' and 'index.html' are normalized
                // to '/'.
                var slashPos = value.LastIndexOf((byte)'/');

                if (slashPos != -1 &&
                    value.Slice(slashPos).StartsWith(_indexHtmBytes))
                {
                    var len = slashPos + 1;
                    var newpattern = new byte[len + 1];
                    value.Slice(0, len).CopyTo(newpattern);
                    newpattern[len] = (byte)'$';
                    HandleAllow(lineNum, newpattern);
                }
            }
        }

        public void HandleDisallow(int lineNum, ReadOnlySpan<byte> value)
        {
            if (!SeenAnyAgent)
                return;
            _seenSeparator = true;
            var priority = LongestMatchRobotsMatchStrategy.MatchDisallowSlow(_path, value);
            if (priority >= 0)
            {
                if (_seenSpecificAgent)
                {
                    if (_disallow.Specific.Priority < priority)
                    {
                        _disallow.Specific.Set(priority);
                    }
                }
                else
                {
                    Debug.Assert(_seenGlobalAgent);
                    if (_disallow.Global.Priority < priority)
                    {
                        _disallow.Global.Set(priority);
                    }
                }
            }
        }

        public void HandleSitemap(int lineNum, ReadOnlySpan<byte> value)
        {
        }

        public void HandleUnknownAction(int lineNum, ReadOnlySpan<byte> action, ReadOnlySpan<byte> value)
        {
        }

        private void InitUserAgentsAndPath(List<byte[]> userAgents, byte[] path)
        {
            _userAgents = userAgents;
            Debug.Assert(path.Length > 0 && path[0] == '/');
            _path = path;
        }

        private bool SeenAnyAgent => _seenGlobalAgent || _seenSpecificAgent;

        public bool AllowedByRobots(byte[] robotsBody, List<byte[]> userAgents, string url)
        {
            // The url is not normalized (escaped, percent encoded) here because the user
            // is asked to provide it in escaped form already.
            var path = GetPathParamsQuery(url);
            return PathAllowedByRobots(robotsBody, userAgents, new UTF8Encoding().GetBytes(path));
        }

        public bool PathAllowedByRobots(byte[] robotsBody, List<byte[]> userAgents, byte[] path)
        {
            InitUserAgentsAndPath(userAgents, path);
            ParseRobotsTxt(robotsBody, this);
            return !Disallow();
        }

        private bool Disallow()
        {
            Debug.Assert(_allow != null);
            Debug.Assert(_disallow != null);

            if (_allow.Specific.Priority > 0 || _disallow.Specific.Priority > 0)
            {
                return (_disallow.Specific.Priority > _allow.Specific.Priority);
            }

            if (_everSeenSpecificAgent)
            {
                // Matching group for user-agent but either without disallow or empty one,
                // i.e. priority == 0.
                return false;
            }

            if (_disallow.Global.Priority > 0 || _allow.Global.Priority > 0)
            {
                return _disallow.Global.Priority > _allow.Global.Priority;
            }

            return false;
        }

        internal static void ParseRobotsTxt(byte[] robotsBody, IRobotsParseHandler parseCallback)
        {
            var parser = new RobotsTxtParser(robotsBody, parseCallback);
            parser.Parse();
        }

        internal static string GetPathParamsQuery(string url)
        {
            var searchStart = 0;
            if (url is ['/', '/', ..]) searchStart = 2;
            var earlyPath = url.IndexOfAny(['/', '?', ';',], searchStart);
            var protocolEnd = url.IndexOf("://", searchStart, StringComparison.Ordinal);
            if (earlyPath < protocolEnd)
            {
                protocolEnd = -1;
            }

            if (protocolEnd == -1)
            {
                protocolEnd = searchStart;
            }
            else
            {
                protocolEnd += 3;
            }

            var pathStart = url.IndexOfAny(['/', '?', ';',], protocolEnd);
            if (pathStart != -1)
            {
                var hashPos = url.IndexOf('#', searchStart);
                if (hashPos >= 0 && hashPos < pathStart) return "/";
                var pathEnd = (hashPos == -1) ? url.Length : hashPos;
                if (url[pathStart] != '/')
                {
                    // Prepend a slash if the result would start e.g. with '?'.
                    return "/" + url.Substring(pathStart, pathEnd - pathStart);
                }

                return url.Substring(pathStart, pathEnd - pathStart);
            }

            return "/";
        }

        private class Match(int priority = Match.NoMatchPriority)
        {
            private const int NoMatchPriority = -1;

            public void Set(int priority)
            {
                Priority = priority;
            }

            public void Clear()
            {
                Set(NoMatchPriority);
            }

            public int Priority { get; private set; } = priority;
        }

        // For each of the directives within user-agents, we keep global and specific
        // match scores.
        class MatchHierarchy
        {
            public readonly Match Global = new Match(); // Match for '*'
            public readonly Match Specific = new Match(); // Match for queried agent.


            public void Clear()
            {
                Global.Clear();
                Specific.Clear();
            }
        }

        readonly MatchHierarchy _allow = new MatchHierarchy(); // Characters of 'url' matching Allow.
        readonly MatchHierarchy _disallow = new MatchHierarchy(); // Characters of 'url' matching Disallow.

        bool _seenGlobalAgent; // True if processing global agent rules.
        bool _seenSpecificAgent; // True if processing our specific agent.
        bool _everSeenSpecificAgent; // True if we ever saw a block for our agent.
        bool _seenSeparator; // True if saw any key: value pair.

        // The path we want to pattern match. Set by InitUserAgentsAndPath.
        byte[]? _path;
        private List<byte[]>? _userAgents; // Set by InitUserAgentsAndPath.

        public bool OneAgentAllowedByRobots(byte[] robotsContent, byte[] userAgent, string url)
        {
            var userAgents = new List<byte[]> { userAgent, };
            return AllowedByRobots(robotsContent, userAgents, url);
        }

        public static bool IsValidUserAgentToObey(Span<byte> userAgent)
        {
            return userAgent.Length > 0 && ExtractUserAgent(userAgent) == userAgent;
        }
        public static bool IsValidUserAgentToObey(string userAgent)
        {
            return IsValidUserAgentToObey(Encoding.UTF8.GetBytes(userAgent));
        }
    }
}
