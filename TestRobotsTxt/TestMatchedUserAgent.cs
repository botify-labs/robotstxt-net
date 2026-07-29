using System.Text;

using RobotsTxt;

using Xunit;

namespace TestRobotsTxt;

// RobotsMachine knows which of the caller's user-agents it obeyed - HandleUserAgent has the token in hand
// at the match site - but until now it kept only the booleans (_seenSpecificAgent, _everSeenSpecificAgent).
// MatchedUserAgent surfaces the token so a caller can record WHICH agent the verdicts came from.
//
// This is an observable only: it must not change a single verdict. The matching state
// (_specificStates/_globalStates) and PathAllowedByRobots are untouched, and TestRobotsMachine plus the
// Google conformance suite are the guard on that.
//
// Semantics being pinned here, because none of them is forced by the name:
//   - null means "no specific group was obeyed", which is exactly PathAllowedByRobots's own fallback
//     condition (_everSeenSpecificAgent false). A caller that speaks robots.txt renders that as "*".
//   - when several groups match, the winner is the LOWEST INDEX in the caller's list, not the first in
//     the file. The caller passes its agents most-specific-first, so this keeps the recorded value on the
//     caller's own priority scale.
//   - it deliberately under-reports: Google obeys the UNION of every matching group, so one token cannot
//     describe a two-group file. One token is what the consumer can use; the loss is documented, not denied.
public class TestMatchedUserAgent
{
    // Most-specific-first, and WITHOUT "*" - the shape RobotsMachine is always constructed with: global
    // groups are tracked separately, in _globalStates.
    private static List<byte[]> Agents() => ["botify"u8.ToArray(), "googlebot"u8.ToArray(),];

    private static string? Matched(string robotsTxt) =>
        new RobotsMachine(Encoding.UTF8.GetBytes(robotsTxt), Agents()).MatchedUserAgent;

    [Fact]
    public void A_global_only_file_matches_no_specific_agent()
    {
        Assert.Null(Matched("""
                            User-agent: *
                            Disallow: /private
                            """));
    }

    [Fact]
    public void An_empty_file_matches_no_specific_agent()
    {
        Assert.Null(Matched(""));
    }

    [Fact]
    public void A_group_for_an_agent_we_do_not_carry_matches_nothing()
    {
        Assert.Null(Matched("""
                            User-agent: bingbot
                            Disallow: /private
                            """));
    }

    [Theory]
    [InlineData("botify")]
    [InlineData("googlebot")]
    public void A_single_specific_group_is_reported(string agent)
    {
        Assert.Equal(agent, Matched($"""
                                     User-agent: *
                                     Disallow: /a

                                     User-agent: {agent}
                                     Disallow: /b
                                     """));
    }

    // The tie-break. Google obeys BOTH groups here; we report the caller's most specific one.
    [Fact]
    public void When_several_groups_match_the_lowest_index_agent_wins()
    {
        Assert.Equal("botify", Matched("""
                                       User-agent: botify
                                       Disallow: /a

                                       User-agent: googlebot
                                       Disallow: /b
                                       """));
    }

    // Same file, groups swapped: the answer must not follow the file's order. Without this, an
    // implementation that simply kept the FIRST match seen would pass the test above and still be wrong.
    [Fact]
    public void File_order_does_not_decide_the_tie()
    {
        Assert.Equal("botify", Matched("""
                                       User-agent: googlebot
                                       Disallow: /b

                                       User-agent: botify
                                       Disallow: /a
                                       """));
    }

    // ExtractUserAgent truncates at the first character outside [a-zA-Z_-], so "botify/2.0" is a botify
    // group. This is the case where Google's tokenisation and a naive whole-value compare disagree, and it
    // is the reason the value has to come from here rather than from a second, hand-rolled scan.
    [Fact]
    public void The_agent_token_is_extracted_the_way_the_matcher_extracts_it()
    {
        Assert.Equal("botify", Matched("""
                                       User-agent: botify/2.0
                                       Disallow: /a
                                       """));
    }

    // ...but truncation is not a prefix match: "botifybot" is all-alpha, so it extracts whole and is a
    // different agent. Pinned because "starts with botify" is the plausible wrong reading of the test above.
    [Fact]
    public void A_longer_agent_name_is_not_a_prefix_match()
    {
        Assert.Null(Matched("""
                            User-agent: botifybot
                            Disallow: /a
                            """));
    }

    // The comparison is case-insensitive, and what is reported is the CALLER's spelling, not the file's -
    // the caller compares this against its own configured list.
    [Fact]
    public void The_reported_token_is_the_callers_spelling()
    {
        Assert.Equal("botify", Matched("""
                                       User-agent: BOTIFY
                                       Disallow: /a
                                       """));
    }

    // A group with no rules still counts: HandleUserAgent sets _everSeenSpecificAgent before any
    // allow/disallow is seen, and PathAllowedByRobots already treats such a group as "obeyed, allows all".
    // Reporting it keeps MatchedUserAgent consistent with the verdict path.
    [Fact]
    public void A_matching_group_with_no_rules_still_counts()
    {
        Assert.Equal("botify", Matched("""
                                       User-agent: *
                                       Disallow: /private

                                       User-agent: botify
                                       """));
    }

    // The observable must not move the verdicts. Same file as the tie-break case: botify is reported, and
    // BOTH groups' disallows still apply, which is the union semantics the single token cannot express.
    [Fact]
    public void Reporting_the_agent_does_not_change_the_verdicts()
    {
        var machine = new RobotsMachine("""
                                        User-agent: botify
                                        Disallow: /a

                                        User-agent: googlebot
                                        Disallow: /b
                                        """u8.ToArray(), Agents());

        Assert.Equal("botify", machine.MatchedUserAgent);
        Assert.False(machine.PathAllowedByRobots("/a"u8.ToArray()));
        Assert.False(machine.PathAllowedByRobots("/b"u8.ToArray()));
        Assert.True(machine.PathAllowedByRobots("/c"u8.ToArray()));
    }
}
