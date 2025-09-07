using System.Runtime.CompilerServices;

namespace RobotsTxt;

public class RobotsMachine : IRobotsParseHandler
{
    class State
    {
    }

    // class StartState : State
    // {
    // }

    class UserAgentState(UserAgentState.UserAgentType type) : State
    {
        // Either store all UAs with their rules, or just the last useful one.
        public enum UserAgentType
        {
            // Unknown,
            Global,
            Specific,
        }

        // Remove?
        public UserAgentType Type { get; } = type;
    }

    class AllowState(byte[] pattern, bool haveWildcards) : State
    {
        public byte[] Pattern { get; } = pattern;
        public bool HaveWildcards { get; } = haveWildcards;
    }

    class DisallowState(byte[] pattern, bool haveWildcards) : State
    {
        public byte[] Pattern { get; } = pattern;
        public bool HaveWildcards { get; } = haveWildcards;
    }

    private readonly List<byte[]> _userAgents;

    private List<State> _globalStates = new();
    private List<State> _specificStates = new();

    private bool _currentAgentIsSpecific = false; // True if we're in a block for our agent.
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

        return userAgent.Slice(0, i);
    }

    public void HandleUserAgent(int lineNum, ReadOnlySpan<byte> userAgent)
    {
        // Google-specific optimization: a '*' followed by space and more characters
        // in a user-agent record is still regarded a global rule.
        if (userAgent.Length >= 1 && userAgent[0] == '*' && (userAgent.Length == 1 || userAgent[1].IsSpace()))
        {
            _globalStates.Add(new UserAgentState(UserAgentState.UserAgentType.Global));
            _currentAgentIsSpecific = false;
            return;
        }
        userAgent = ExtractUserAgent(userAgent);
        foreach (var ua in _userAgents)
        {
            if (!userAgent.EqualsIgnoreCase(ua)) continue;
            _specificStates.Add(new UserAgentState(UserAgentState.UserAgentType.Specific));
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
        if (allowHierarchy.Priority > 0 || disallowHierarchy.Priority > 0)
        {
            return (disallowHierarchy.Priority > allowHierarchy.Priority);
        }

        if (EverSeenSpecificAgent)
        {
            // Matching group for user-agent but either without disallow or empty one,
            // i.e. priority == 0.
            return false;
        }

        (allowHierarchy, disallowHierarchy) = AssessAccessRules(path, _globalStates);

        if (disallowHierarchy.Priority > 0 || allowHierarchy.Priority > 0)
        {
            return disallowHierarchy.Priority > allowHierarchy.Priority;
        }

        return false;
    }

    private (Match, Match) AssessAccessRules(byte[] path, List<State> states)
    {
        Match allowHierarchy = new(); // Characters of 'url' matching Allow.
        Match disallowHierarchy = new(); // Characters of 'url' matching Disallow.
        foreach (var state in states)
        {
            switch (state)
            {
                case AllowState allow:
                    CheckAllow(path, allow.Pattern, allow.HaveWildcards, allowHierarchy);
                    break;
                case DisallowState disallow:
                    CheckDisallow(path, disallow.Pattern, disallow.HaveWildcards, disallowHierarchy);
                    break;
            }
        }
        return (allowHierarchy, disallowHierarchy);
    }

    private class Match(int priority = Match.NoMatchPriority)
    {
        private const int NoMatchPriority = -1;

        public void Clear()
        {
            Priority = NoMatchPriority;
        }

        public int Priority { get; set; } = priority;
    }

    readonly byte[] _indexHtmBytes = "/index.htm"u8.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckAllow(byte[] path, ReadOnlySpan<byte> pattern, bool haveWildcards, Match allow)
    {
        var priority = LongestMatchRobotsMatchStrategy.MatchAllowFast(path, pattern, haveWildcards);
        if (priority >= 0)
        {
            if (allow.Priority < priority)
            {
                allow.Priority = priority;
            }
        }
        else
        {
            // Google-specific optimization: 'index.htm' and 'index.html' are normalized
            // to '/'.
            var slashPos = pattern.LastIndexOf((byte)'/');

            if (slashPos != -1 &&
                pattern.Slice(slashPos).StartsWith(_indexHtmBytes))
            {
                var len = slashPos + 1;
                var newpattern = new byte[len + 1];
                pattern.Slice(0, len).CopyTo(newpattern);
                newpattern[len] = (byte)'$';
                CheckAllow(path, newpattern, true, allow);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisallow(byte[] path, ReadOnlySpan<byte> value, bool haveWildcards, Match disallow)
    {
        var priority = LongestMatchRobotsMatchStrategy.MatchDisallowFast(path, value, haveWildcards);
        if (priority >= 0)
        {
            if (disallow.Priority < priority)
            {
                disallow.Priority = priority;
            }
        }
    }
}
