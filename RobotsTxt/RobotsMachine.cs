using System.Runtime.CompilerServices;

namespace RobotsTxt;

public class RobotsMachine : IRobotsParseHandler
{
    private const int NoMatchPriority = -1;

    private class State;

    private class UserAgentState : State;

    private class AllowState(ReadOnlyMemory<byte> pattern, bool fastPath) : State
    {
        public ReadOnlyMemory<byte> Pattern { get; } = pattern;
        public bool FastPath { get; } = fastPath;
    }

    private class DisallowState(ReadOnlyMemory<byte> pattern, bool fastPath) : State
    {
        public ReadOnlyMemory<byte> Pattern { get; } = pattern;
        public bool FastPath { get; } = fastPath;
    }

    private readonly List<byte[]> _userAgents;

    private readonly List<State> _globalStates = [];
    private readonly List<State> _specificStates = [];

    private bool _seenSpecificAgent; // True if we're in a block for our agent.
    private bool _seenGlobalAgent; // True if we're in a block for global agent.
    private bool _everSeenSpecificAgent; // True if we ever saw a block for our agent.
    private bool _seenSeparator; // True if saw any key: value pair (key: allow/disallow).

    private bool CurrentAgentIsSignificant => _seenSpecificAgent || _seenGlobalAgent;
    private bool SeenAnyAgent => _everSeenSpecificAgent || _globalStates.Count > 0;

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
        if (_seenSeparator)
        {
            // Needed to handle a serie of User-Agent: lines containing our agent.
            _seenSpecificAgent = _seenGlobalAgent = _seenSeparator = false;
        }

        // Google-specific optimization: a '*' followed by space and more characters
        // in a user-agent record is still regarded a global rule.
        if (userAgent.Length >= 1 && userAgent[0] == '*' && (userAgent.Length == 1 || userAgent[1].IsSpace()))
        {
            _globalStates.Add(new UserAgentState());
            _seenGlobalAgent = true;
            return;
        }
        userAgent = ExtractUserAgent(userAgent);
        foreach (var ua in _userAgents)
        {
            if (userAgent.Length != ua.Length) continue;
            bool match = true;
            for (int i = 0; i < ua.Length; i++)
            {
                byte a = userAgent[i];
                byte b = ua[i];
                if (a == b || (a >= 'A' && a <= 'Z' && a + 32 == b) || (b >= 'A' && b <= 'Z' && b + 32 == a))
                    continue;
                match = false;
                break;
            }
            if (!match) continue;
            _specificStates.Add(new UserAgentState());
            _everSeenSpecificAgent = _seenSpecificAgent = true;
            return;
        }
    }

    public void HandleAllow(int lineNum, ReadOnlySpan<byte> value)
    {
        if (!CurrentAgentIsSignificant)
            return;
        _seenSeparator = true;

        var fastPath = !value.ContainsAny("*$"u8);

        AllowState? rootState = null;
        // Google-specific optimization: 'index.htm' and 'index.html' are normalized
        // to '/'.
        var slashPos = value.LastIndexOf((byte)'/');
        if (slashPos != -1 && value[slashPos..].StartsWith(IndexHtmBytes))
        {
            var len = slashPos + 1;
            var newValue = new byte[len + 1];
            value[..len].CopyTo(newValue);
            newValue[len] = (byte)'$';
            rootState = new AllowState(newValue, false);
        }

        var state = new AllowState(value.ToArray(), fastPath);
        if (_seenSpecificAgent)
        {
            _specificStates.Add(state);
            if (rootState != null)
            {
                _specificStates.Add(rootState);
            }
        }
        if (_seenGlobalAgent)
        {
            _globalStates.Add(state);
            if (rootState != null)
            {
                _globalStates.Add(rootState);
            }
        }
    }

    public void HandleDisallow(int lineNum, ReadOnlySpan<byte> value)
    {
        if (!CurrentAgentIsSignificant)
            return;
        _seenSeparator = true;

        var fastPath = !value.ContainsAny("*$"u8);
        var state = new DisallowState(value.ToArray(), fastPath);
        if (_seenSpecificAgent)
            _specificStates.Add(state);
        if (_seenGlobalAgent)
            _globalStates.Add(state);
    }

    public void HandleSitemap(int lineNum, ReadOnlySpan<byte> value)
    {
    }

    public void HandleUnknownAction(int lineNum, ReadOnlySpan<byte> action, ReadOnlySpan<byte> value)
    {
    }

    public bool PathAllowedByRobots(byte[] path)
    {
        return !Disallow();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Disallow()
        {
            if (!SeenAnyAgent)
                return false;

            var (allowHierarchy, disallowHierarchy) = AssessAccessRules(path, _specificStates);
            if (allowHierarchy > 0 || disallowHierarchy > 0)
            {
                return disallowHierarchy > allowHierarchy;
            }

            if (_everSeenSpecificAgent)
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int, int) AssessAccessRules(byte[] path, List<State> states)
    {
        var allowHierarchy = NoMatchPriority; // Characters of 'url' matching Allow.
        var disallowHierarchy = NoMatchPriority; // Characters of 'url' matching Disallow.

        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            switch (state)
            {
                case AllowState allow:
                    allowHierarchy = Check(path, allow.Pattern.Span, allow.FastPath, allowHierarchy);
                    break;
                case DisallowState disallow:
                    disallowHierarchy = Check(path, disallow.Pattern.Span, disallow.FastPath, disallowHierarchy);
                    break;
            }
        }
        return (allowHierarchy, disallowHierarchy);
    }

    private static readonly byte[] IndexHtmBytes = "/index.htm"u8.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Check(byte[] path, ReadOnlySpan<byte> pattern, bool fastPath, int currentPriority)
    {
        var priority = LongestMatchRobotsMatchStrategy.MatchFast(path, pattern, fastPath);
        if (priority < 0) return currentPriority;
        if (currentPriority < priority)
        {
            currentPriority = priority;
        }
        return currentPriority;
    }
}
