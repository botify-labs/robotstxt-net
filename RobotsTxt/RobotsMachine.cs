using System.Runtime.CompilerServices;

namespace RobotsTxt;

public class RobotsMachine : IRobotsParseHandler
{
    private const int NoMatchPriority = -1;

    private class State;

    private class UserAgentState : State;

    private class AllowState(byte[] pattern, bool haveWildcards) : State
    {
        public byte[] Pattern { get; } = pattern;
        public bool HaveWildcards { get; } = haveWildcards;
    }

    private class DisallowState(byte[] pattern, bool haveWildcards) : State
    {
        public byte[] Pattern { get; } = pattern;
        public bool HaveWildcards { get; } = haveWildcards;
    }

    private readonly List<byte[]> _userAgents;

    private readonly List<State> _globalStates = [];
    private readonly List<State> _specificStates = [];

    private bool _currentAgentIsSpecific; // True if we're in a block for our agent.
    private bool EverSeenSpecificAgent => _specificStates.Count > 0;

    public RobotsMachine(byte[] robotsBody, List<byte[]> userAgents)
    {
        _userAgents = userAgents;
        ParseRobotsTxt(robotsBody, this);
    }

    private static void ParseRobotsTxt(byte[] robotsBody, IRobotsParseHandler parseCallback)
    {
        var parser = new RobotsTxtParser(robotsBody, parseCallback);
        parser.Parse();
    }

    public void HandleRobotsStart()
    {
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

        return userAgent[..i];
    }

    public void HandleUserAgent(int lineNum, ReadOnlySpan<byte> userAgent)
    {
        // Google-specific optimization: a '*' followed by space and more characters
        // in a user-agent record is still regarded a global rule.
        if (userAgent.Length >= 1 && userAgent[0] == '*' && (userAgent.Length == 1 || userAgent[1].IsSpace()))
        {
            _globalStates.Add(new UserAgentState());
            _currentAgentIsSpecific = false;
            return;
        }
        userAgent = ExtractUserAgent(userAgent);
        foreach (var ua in _userAgents)
        {
            if (!userAgent.EqualsIgnoreCase(ua)) continue;
            _specificStates.Add(new UserAgentState());
            _currentAgentIsSpecific = true;
            return;
        }
    }

    private bool SeenAnyAgent => _specificStates.Count > 0 || _globalStates.Count > 0;

    public void HandleAllow(int lineNum, ReadOnlySpan<byte> value)
    {
        if (!SeenAnyAgent)
            return;
        var states = _currentAgentIsSpecific ? _specificStates : _globalStates;
        var haveWildcards = value.Length >= 1 && (value.Contains((byte)'*') || value[^1] == '$');
        states.Add(new AllowState(value.ToArray(), haveWildcards));
    }
    public void HandleDisallow(int lineNum, ReadOnlySpan<byte> value)
    {
        if (!SeenAnyAgent)
            return;
        var states = _currentAgentIsSpecific ? _specificStates : _globalStates;
        var haveWildcards = value.Length >= 1 && (value.Contains((byte)'*') || value[^1] == '$');
        states.Add(new DisallowState(value.ToArray(), haveWildcards));
    }

    public void HandleSitemap(int lineNum, ReadOnlySpan<byte> value)
    {
    }

    public void HandleUnknownAction(int lineNum, ReadOnlySpan<byte> action, ReadOnlySpan<byte> value)
    {
    }

    public bool PathAllowedByRobots(byte[] path)
    {
        return !Disallow(path);
    }

    private bool Disallow(byte[] path)
    {
        if (!SeenAnyAgent)
            return false;

        var (allowHierarchy, disallowHierarchy) = AssessAccessRules(path, _specificStates);
        if (allowHierarchy > 0 || disallowHierarchy > 0)
        {
            return disallowHierarchy > allowHierarchy;
        }

        if (EverSeenSpecificAgent)
        {
            // Matching group for user-agent but either without disallow or empty one,
            // i.e. priority == 0.
            return false;
        }

        (allowHierarchy, disallowHierarchy) = AssessAccessRules(path, _globalStates);

        if (disallowHierarchy > 0 || allowHierarchy > 0)
        {
            return disallowHierarchy > allowHierarchy;
        }

        return false;
    }

    private static (int, int) AssessAccessRules(byte[] path, List<State> states)
    {
        var allowHierarchy = NoMatchPriority; // Characters of 'url' matching Allow.
        var disallowHierarchy = NoMatchPriority; // Characters of 'url' matching Disallow.

        foreach (var state in states)
        {
            switch (state)
            {
                case AllowState allow:
                    allowHierarchy = CheckAllow(path, allow.Pattern, allow.HaveWildcards, allowHierarchy);
                    break;
                case DisallowState disallow:
                    disallowHierarchy = CheckDisallow(path, disallow.Pattern, disallow.HaveWildcards, disallowHierarchy);
                    break;
            }
        }
        return (allowHierarchy, disallowHierarchy);
    }

    private static readonly byte[] IndexHtmBytes = "/index.htm"u8.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CheckAllow(byte[] path, ReadOnlySpan<byte> pattern, bool haveWildcards, int allow)
    {
        while (true)
        {
            var priority = LongestMatchRobotsMatchStrategy.MatchAllowFast(path, pattern, haveWildcards);
            if (priority >= 0)
            {
                if (allow < priority)
                {
                    allow = priority;
                }
            }
            else
            {
                // Google-specific optimization: 'index.htm' and 'index.html' are normalized
                // to '/'.
                var slashPos = pattern.LastIndexOf((byte)'/');

                if (slashPos != -1 && pattern[slashPos..].StartsWith(IndexHtmBytes))
                {
                    var len = slashPos + 1;
                    var newpattern = new byte[len + 1];
                    pattern[..len].CopyTo(newpattern);
                    newpattern[len] = (byte)'$';
                    pattern = newpattern;
                    haveWildcards = true;
                    continue;
                }
            }
            break;
        }
        return allow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CheckDisallow(byte[] path, ReadOnlySpan<byte> value, bool haveWildcards, int disallow)
    {
        var priority = LongestMatchRobotsMatchStrategy.MatchDisallowFast(path, value, haveWildcards);
        if (priority < 0) return disallow;
        if (disallow < priority)
        {
            disallow = priority;
        }
        return disallow;
    }
}
