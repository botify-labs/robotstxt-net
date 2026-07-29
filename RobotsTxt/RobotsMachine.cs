using System.Runtime.CompilerServices;
using System.Text;

namespace RobotsTxt;

public class RobotsMachine : IRobotsParseHandler
{
    private const int NoMatchPriority = -1;

    private class State;

    private class UserAgentState : State;

    private class AllowState(ReadOnlyMemory<byte> pattern, bool isSimplePattern) : State
    {
        public ReadOnlyMemory<byte> Pattern { get; } = pattern;
        public bool IsSimplePattern { get; } = isSimplePattern;
    }

    private class DisallowState(ReadOnlyMemory<byte> pattern, bool isSimplePattern) : State
    {
        public ReadOnlyMemory<byte> Pattern { get; } = pattern;
        public bool IsSimplePattern { get; } = isSimplePattern;
    }

    private readonly List<byte[]> _userAgents;

    private readonly List<State> _globalStates = [];
    private readonly List<State> _specificStates = [];

    private bool _seenSpecificAgent; // True if we're in a block for our agent.
    private bool _seenGlobalAgent; // True if we're in a block for global agent.
    private bool _everSeenSpecificAgent; // True if we ever saw a block for our agent.
    private bool _seenSeparator; // True if saw any key: value pair (key: allow/disallow).

    // Lowest index in _userAgents among the groups we obeyed, or -1. Written only during the parse, which
    // the constructor runs to completion, so MatchedUserAgent is fixed by the time anyone can observe it.
    private int _matchedAgentIdx = -1;

    /// <summary>
    /// Which of the caller's user-agents these verdicts were built from, or null if no specific group
    /// matched and only the global ("*") rules apply - the same condition PathAllowedByRobots falls back on.
    /// The value is the CALLER's spelling, not the file's, so it can be compared against the list passed in.
    /// <para>
    /// Reporting only: nothing here is read back by the matching. _specificStates, _globalStates and
    /// PathAllowedByRobots are untouched by the change that added it, and the suite - Google conformance
    /// included - passes unchanged. That is the evidence for "no verdict moved"; it is not a proof.
    /// </para>
    /// <para>
    /// When several groups match, this is the lowest-index one - callers pass their agents
    /// most-specific-first, so this is "the most specific agent present". It deliberately under-reports:
    /// the rules actually obeyed are the UNION of every matching group (see _specificStates), and one token
    /// cannot describe a two-group file.
    /// </para>
    /// </summary>
    public string? MatchedUserAgent { get; }

    private bool CurrentAgentIsSignificant => _seenSpecificAgent || _seenGlobalAgent;
    private bool SeenAnyAgent => _everSeenSpecificAgent || _globalStates.Count > 0;

    public RobotsMachine(byte[] robotsBody, List<byte[]> userAgents)
    {
        _userAgents = userAgents;
        ParseRobotsTxt(robotsBody, this);
        // Decoded here rather than in the property: the parse is done, so the value can be readonly and
        // costs one allocation per file instead of one per read.
        if (_matchedAgentIdx >= 0)
        {
            MatchedUserAgent = Encoding.UTF8.GetString(_userAgents[_matchedAgentIdx]);
        }
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
        // Indexed rather than foreach so the match site can compare specificity: _userAgents is ordered
        // most-specific-first by the caller, so a lower index is a more specific agent.
        for (var agentIdx = 0; agentIdx < _userAgents.Count; agentIdx++)
        {
            var ua = _userAgents[agentIdx];
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
            // Min, not first-wins: a file may carry several groups we obey, in any order, and the answer
            // must come from the caller's priority order rather than the file's layout.
            if (_matchedAgentIdx < 0 || agentIdx < _matchedAgentIdx)
            {
                _matchedAgentIdx = agentIdx;
            }
            return;
        }
    }

    public void HandleAllow(int lineNum, ReadOnlySpan<byte> value)
    {
        if (!CurrentAgentIsSignificant)
            return;
        _seenSeparator = true;

        var isSimplePattern = !value.ContainsAny("*$"u8);

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

        var state = new AllowState(value.ToArray(), isSimplePattern);
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

        var isSimplePattern = !value.ContainsAny("*$"u8);
        var state = new DisallowState(value.ToArray(), isSimplePattern);
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
                    allowHierarchy = Check(path, allow.Pattern.Span, allow.IsSimplePattern, allowHierarchy);
                    break;
                case DisallowState disallow:
                    disallowHierarchy = Check(path, disallow.Pattern.Span, disallow.IsSimplePattern, disallowHierarchy);
                    break;
            }
        }
        return (allowHierarchy, disallowHierarchy);
    }

    private static readonly byte[] IndexHtmBytes = "/index.htm"u8.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Check(byte[] path, ReadOnlySpan<byte> pattern, bool isSimplePattern, int currentPriority)
    {
        var priority = LongestMatchRobotsMatchStrategy.MatchFast(path, pattern, isSimplePattern);
        if (priority < 0) return currentPriority;
        if (currentPriority < priority)
        {
            currentPriority = priority;
        }
        return currentPriority;
    }
}
