using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using RobotsTxt;
using Xunit;

namespace TestRobotsTxt
{
    public class GoogleTests
    {
        bool IsUserAgentAllowed(string robotsTxt, string userAgent, string url)
        {
            RobotsMatcher matcher = new RobotsMatcher();
            return matcher.OneAgentAllowedByRobots(Encoding.UTF8.GetBytes(robotsTxt), userAgent, url);
        }

        // Google-specific: system test.
        [Theory]
        [InlineData("", "FooBot", true)] // Empty robots.txt: everything allowed.
        [InlineData(
            "user-agent: FooBot\n" +
            "disallow: /\n", "", true
        )] // Empty user-agent to be matched: everything allowed.
        [InlineData(
            "user-agent: FooBot\n" +
            "disallow: /\n",
            "FooBot",
            false)] // Empty url: implicitly disallowed, see method comment for GetPathParamsQuery in robots.cc.
        [InlineData("", "", true)] // All params empty: same as robots.txt empty, everything allowed.
        public void GoogleOnly_SystemTest(string robotsTxt, string userAgent, bool expected)
        {
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, userAgent, ""));
        }

        // Rules are colon separated name-value pairs. The following names are
        // provisioned:
        //     user-agent: <value>
        //     allow: <value>
        //     disallow: <value>
        // See REP RFC section "Protocol Definition".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.1
        //
        // Google specific: webmasters sometimes miss the colon separator, but it's
        // obvious what they mean by "disallow /", so we assume the colon if it's
        // missing.
        [Theory]
        [InlineData(
            "user-agent: FooBot\n" +
            "disallow: /\n",
            false)]
        [InlineData(
            "foo: FooBot\n" +
            "bar: /\n",
            true
        )]
        [InlineData(
            "user-agent FooBot\n" +
            "disallow /\n",
            false
        )]
        public void ID_LineSyntax_Line(string robotsTxt, bool expected)
        {
            var url = "http://foo.bar/x/y";

            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, "FooBot", url));
        }

        // A group is one or more user-agent line followed by rules, and terminated
        // by a another user-agent line. Rules for same user-agents are combined
        // opaquely into one group. Rules outside groups are ignored.
        // See REP RFC section "Protocol Definition".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.1
        [Theory]
        [InlineData("FooBot", "http://foo.bar/x/b", true)]
        [InlineData("FooBot", "http://foo.bar/z/d", true)]
        [InlineData("FooBot", "http://foo.bar/y/c", false)]
        [InlineData("BarBot", "http://foo.bar/y/c", true)]
        [InlineData("BarBot", "http://foo.bar/w/a", true)]
        [InlineData("BarBot", "http://foo.bar/z/d", false)]
        [InlineData("BazBot", "http://foo.bar/z/d", true)]
        // Lines with rules outside groups are ignored.
        [InlineData("FooBot", "http://foo.bar/foo/bar/", false)]
        [InlineData("BarBot", "http://foo.bar/foo/bar/", false)]
        [InlineData("BazBot", "http://foo.bar/foo/bar/", false)]
        public void ID_LineSyntax_Groups(string userAgent, string url, bool expected)
        {
            var robotstxt =
                "allow: /foo/bar/\n" +
                "\n" +
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /x/\n" +
                "user-agent: BarBot\n" +
                "disallow: /\n" +
                "allow: /y/\n" +
                "\n" +
                "\n" +
                "allow: /w/\n" +
                "user-agent: BazBot\n" +
                "\n" +
                "user-agent: FooBot\n" +
                "allow: /z/\n" +
                "disallow: /\n";

            Assert.Equal(expected, IsUserAgentAllowed(robotstxt, userAgent, url));
        }

        // Group must not be closed by rules not explicitly defined in the REP RFC.
        // See REP RFC section "Protocol Definition".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.1
        [Theory]
        [InlineData("FooBot", false)]
        [InlineData("BarBot", false)]
        public void ID_LineSyntax_Groups_OtherRules(string userAgent, bool expected)
        {
            var robotsTxt =
                "User-agent: BarBot\n" +
                "Sitemap: https://foo.bar/sitemap\n" +
                "User-agent: *\n" +
                "Disallow: /\n";
            var url = "http://foo.bar/";
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, userAgent, url));
        }

        [Theory]
        [InlineData("FooBot", false)]
        [InlineData("BarBot", false)]
        public void ID_LineSyntax_Groups_OtherRules_2(string userAgent, bool expected)
        {
            var robotsTxt =
                "User-agent: FooBot\n" +
                "Invalid-Unknown-Line: unknown\n" +
                "User-agent: *\n" +
                "Disallow: /\n";
            var url = "http://foo.bar/";
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, userAgent, url));
        }

        // REP lines are case insensitive. See REP RFC section "Protocol Definition".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.1
        [Theory]
        [InlineData("http://foo.bar/x/y", true)]
        [InlineData("http://foo.bar/a/b", false)]
        public void ID_REPLineNamesCaseInsensitive(string url, bool expected)
        {
            const string robotsTxtUpper = "USER-AGENT: FooBot\n" +
                                          "ALLOW: /x/\n" +
                                          "DISALLOW: /\n";
            const string robotsTxtLower = "user-agent: FooBot\n" +
                                          "allow: /x/\n" +
                                          "disallow: /\n";
            const string robotsTxtCamel = "uSeR-aGeNt: FooBot\n" +
                                          "AlLoW: /x/\n" +
                                          "dIsAlLoW: /\n";

            Assert.Equal(expected, IsUserAgentAllowed(robotsTxtUpper, "FooBot", url));
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxtLower, "FooBot", url));
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxtCamel, "FooBot", url));
        }

        // A user-agent line is expected to contain only [a-zA-Z_-] characters and must
        // not be empty. See REP RFC section "The user-agent line".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.1
        [Theory]
        [InlineData("Foobot", true)]
        [InlineData("Foo_Bar", true)]
        [InlineData("Foobot-Bar", true)]
        [InlineData("", false)]
        [InlineData("ツ", false)]
        [InlineData("Foobot*", false)]
        [InlineData(" Foobot ", false)]
        [InlineData("Foobot/2.1", false)]
        [InlineData("Foobot Bar", false)]
        public void ID_VerifyValidUserAgentsToObey(string userAgent, bool expected)
        {
            Assert.Equal(expected, RobotsMatcher.IsValidUserAgentToObey(userAgent));
        }

        // User-agent line values are case insensitive. See REP RFC section "The
        // user-agent line".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.1
        [Fact]
        public void ID_UserAgentValueCaseInsensitive()
        {
            const string robotstxtUpper = "User-Agent: FOO BAR\n" +
                                          "Allow: /x/\n" +
                                          "Disallow: /\n";
            const string robotstxtLower = "User-Agent: foo bar\n" +
                                          "Allow: /x/\n" +
                                          "Disallow: /\n";
            const string robotstxtCamel = "User-Agent: FoO bAr\n" +
                                          "Allow: /x/\n" +
                                          "Disallow: /\n";
            const string urlAllowed = "http://foo.bar/x/y";
            const string urlDisallowed = "http://foo.bar/a/b";

            Assert.True(IsUserAgentAllowed(robotstxtUpper, "Foo", urlAllowed));
            Assert.True(IsUserAgentAllowed(robotstxtLower, "Foo", urlAllowed));
            Assert.True(IsUserAgentAllowed(robotstxtCamel, "Foo", urlAllowed));
            Assert.False(IsUserAgentAllowed(robotstxtUpper, "Foo", urlDisallowed));
            Assert.False(IsUserAgentAllowed(robotstxtLower, "Foo", urlDisallowed));
            Assert.False(IsUserAgentAllowed(robotstxtCamel, "Foo", urlDisallowed));
            Assert.True(IsUserAgentAllowed(robotstxtUpper, "foo", urlAllowed));
            Assert.True(IsUserAgentAllowed(robotstxtLower, "foo", urlAllowed));
            Assert.True(IsUserAgentAllowed(robotstxtCamel, "foo", urlAllowed));
            Assert.False(IsUserAgentAllowed(robotstxtUpper, "foo", urlDisallowed));
            Assert.False(IsUserAgentAllowed(robotstxtLower, "foo", urlDisallowed));
            Assert.False(IsUserAgentAllowed(robotstxtCamel, "foo", urlDisallowed));
        }

        // Google specific: accept user-agent value up to the first space. Space is not
        // allowed in user-agent values, but that doesn't stop webmasters from using
        // them. This is more restrictive than the RFC, since in case of the bad value
        // "Googlebot Images" we'd still obey the rules with "Googlebot".
        // Extends REP RFC section "The user-agent line"
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.1
        [Theory]
        [InlineData("Foo", true)]
        [InlineData("Foo Bar", false)]
        public void GoogleOnly_AcceptUserAgentUpToFirstSpace(string userAgent, bool expected)
        {
            Assert.False(RobotsMatcher.IsValidUserAgentToObey("Foobot Bar"));
            var robotsTxt =
                "User-Agent: *\n" +
                "Disallow: /\n" +
                "User-Agent: Foo Bar\n" +
                "Allow: /x/\n" +
                "Disallow: /\n";

            var url = "http://foo.bar/x/y";

            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, userAgent, url));
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, userAgent, url));
        }

        // If no group matches the user-agent, crawlers must obey the first group with a
        // user-agent line with a "*" value, if present. If no group satisfies either
        // condition, or no groups are present at all, no rules apply.
        // See REP RFC section "The user-agent line".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.1
        [Fact]
        public void ID_GlobalGroups_Secondary()
        {
            const string robotsTxtEmpty = "";
            const string robotsTxtGlobal = "user-agent: *\n" +
                                           "allow: /\n" +
                                           "user-agent: FooBot\n" +
                                           "disallow: /\n";
            const string robotsTxtOnlySpecific = "user-agent: FooBot\n" +
                                                 "allow: /\n" +
                                                 "user-agent: BarBot\n" +
                                                 "disallow: /\n" +
                                                 "user-agent: BazBot\n" +
                                                 "disallow: /\n";
            var url = "http://foo.bar/x/y";

            Assert.True(IsUserAgentAllowed(robotsTxtEmpty, "FooBot", url));
            Assert.False(IsUserAgentAllowed(robotsTxtGlobal, "FooBot", url));
            Assert.True(IsUserAgentAllowed(robotsTxtGlobal, "BarBot", url));
            Assert.True(IsUserAgentAllowed(robotsTxtOnlySpecific, "QuxBot", url));
        }

        // Matching rules against URIs is case sensitive.
        // See REP RFC section "The Allow and Disallow lines".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.2
        [Fact]
        public void ID_AllowDisallow_Value_CaseSensitive()
        {
            const string robotsTxtLowercaseUrl = "user-agent: FooBot\n" +
                                                 "disallow: /x/\n";
            const string robotsTxtUppercaseUrl = "user-agent: FooBot\n" +
                                                 "disallow: /X/\n";
            var url = "http://foo.bar/x/y";

            Assert.False(IsUserAgentAllowed(robotsTxtLowercaseUrl, "FooBot", url));
            Assert.True(IsUserAgentAllowed(robotsTxtUppercaseUrl, "FooBot", url));
        }

        // The most specific match found MUST be used. The most specific match is the
        // match that has the most octets. In case of multiple rules with the same
        // length, the least strict rule must be used.
        // See REP RFC section "The Allow and Disallow lines".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.2
        [Fact]
        public void ID_LongestMatch()
        {
            var url = "http://foo.bar/x/page.html";
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /x/page.html\n" +
                "allow: /x/\n";

            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /x/page.html\n" +
                "disallow: /x/\n";

            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/x/"));

            robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: \n" +
                "allow: \n";
            // In case of equivalent disallow and allow patterns for the same
            // user-agent, allow is used.
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /\n";
            // In case of equivalent disallow and allow patterns for the same
            // user-agent, allow is used.
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            const string urlA = "http://foo.bar/x";
            const string urlB = "http://foo.bar/x/";
            robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /x\n" +
                "allow: /x/\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", urlA));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", urlB));

            robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /x/page.html\n" +
                "allow: /x/page.html\n";
            // In case of equivalent disallow and allow patterns for the same
            // user-agent, allow is used.
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /page\n" +
                "disallow: /*.html\n";
            // Longest match wins.
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/page.html"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/page"));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /x/page.\n" +
                "disallow: /*.html\n";
            // Longest match wins.
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/x/y.html"));

            robotsTxt =
                "User-agent: *\n" +
                "Disallow: /x/\n" +
                "User-agent: FooBot\n" +
                "Disallow: /y/\n";
            // Most specific group for FooBot allows implicitly /x/page.
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/x/page"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/y/page"));
        }


        // Octets in the URI and robots.txt paths outside the range of the US-ASCII
        // coded character set, and those in the reserved range defined by RFC3986,
        // MUST be percent-encoded as defined by RFC3986 prior to comparison.
        // See REP RFC section "The Allow and Disallow lines".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.2
        //
        // NOTE: It's up to the caller to percent encode a URL before passing it to the
        // parser. Percent encoding URIs in the rules is unnecessary.
        [Theory]
        [InlineData("User-agent: FooBot\n" +
                    "Disallow: /\n" +
                    "Allow: /foo/bar?qux=taz&baz=http://foo.bar?tar&par\n",
            "http://foo.bar/foo/bar?qux=taz&baz=http://foo.bar?tar&par",
            true
        )]
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/ツ\n",
            "http://foo.bar/foo/bar/%E3%83%84",
            true
        )] // 3 byte character: /foo/bar/ツ -> /foo/bar/%E3%83%84
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/ツ\n",
            "http://foo.bar/foo/bar/ツ",
            false
        )] // The parser encodes the 3-byte character, but the URL is not %-encoded.
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/%E3%83%84\n",
            "http://foo.bar/foo/bar/%E3%83%84",
            true
        )] // Percent encoded 3 byte character: /foo/bar/%E3%83%84 -> /foo/bar/%E3%83%84
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/%E3%83%84\n",
            "http://foo.bar/foo/bar/ツ",
            false
        )] // Percent encoded 3 byte character: /foo/bar/%E3%83%84 -> /foo/bar/%E3%83%84
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/%62%61%7A\n",
            "http://foo.bar/foo/bar/baz",
            false
        )] // Percent encoded unreserved US-ASCII: /foo/bar/%62%61%7A -> NULL
        // This is illegal according to RFC3986 and while it may work here due to
        // simple string matching, it should not be relied on.
        [InlineData(
            "User-agent: FooBot\n" +
            "Disallow: /\n" +
            "Allow: /foo/bar/%62%61%7A\n",
            "http://foo.bar/foo/bar/%62%61%7A",
            true
        )]
        public void ID_Encoding(string robotsTxt, string url, bool expected)
        {
            Assert.Equal(expected, IsUserAgentAllowed(robotsTxt, "FooBot", url));
        }

        // The REP RFC defines the following characters that have special meaning in
        // robots.txt:
        // # - inline comment.
        // $ - end of pattern.
        // * - any number of characters.
        // See REP RFC section "Special Characters".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.3
        [Fact]
        public void ID_SpecialCharacters()
        {
            var robotsTxt =
                "User-agent: FooBot\n" +
                "Disallow: /foo/bar/quz\n" +
                "Allow: /foo/*/qux\n";

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar/quz"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/quz"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo//quz"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bax/quz"));

            robotsTxt =
                "User-agent: FooBot\n" +
                "Disallow: /foo/bar$\n" +
                "Allow: /foo/bar/qux\n";
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar/qux"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar/"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar/baz"));

            robotsTxt =
                "User-agent: FooBot\n" +
                "# Disallow: /\n" +
                "Disallow: /foo/quz#qux\n" +
                "Allow: /\n";
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/quz"));
        }


        // Google-specific: "index.html" (and only that) at the end of a pattern is
        // equivalent to "/".
        [Theory]
        [InlineData("http://foo.com/allowed-slash/",
            true)] // If index.html is allowed, we interpret this as / being allowed too.
        [InlineData("http://foo.com/allowed-slash/index.htm", false)] // Does not exactly match.
        [InlineData("http://foo.com/allowed-slash/index.html", true)] // Exact match.
        [InlineData("http://foo.com/anyother-url", false)]
        public void GoogleOnly_IndexHTMLisDirectory(string url, bool expected)
        {
            var robotstxt =
                "User-Agent: *\n" +
                "Allow: /allowed-slash/index.html\n" +
                "Disallow: /\n";

            var isUserAgentAllowed = IsUserAgentAllowed(robotstxt, "foobot", url);
            Assert.Equal(expected, isUserAgentAllowed);
        }

        [Fact]
        public void GoogleOnly_LineTooLong_1()
        {
            var eolLen = "\n".Length;
            const int maxLineLen = 2083 * 8 + 1;
            const string disallow = "disallow: ";

            var robotsTxt = new StringBuilder("user-agent: FooBot\n");

            var longline = new StringBuilder("/x/");
            var maxLength = maxLineLen - longline.Length - disallow.Length + eolLen;
            while (longline.Length < maxLength)
            {
                longline.Append("a");
            }

            robotsTxt.Append(disallow).Append(longline).Append("/qux\n");

            // Matches nothing, so URL is allowed.
            Assert.True(IsUserAgentAllowed(robotsTxt.ToString(), "FooBot", "http://foo.bar/fux"));
            // Matches cut off disallow rule.
            var actual = IsUserAgentAllowed(robotsTxt.ToString(), "FooBot", "http://foo.bar" + longline + "/fux");
            Assert.False(actual);
        }

        [Fact]
        public void GoogleOnly_LineTooLong_2()
        {
            var eolLen = "\n".Length;
            const int maxLineLen = 2083 * 8 + 1;
            var allow = "allow: ";

            var robotsTxt = new StringBuilder(
                "user-agent: FooBot\n" +
                "disallow: /\n");

            var longlineA = new StringBuilder("/a/");
            var longlineB = new StringBuilder("/b/");
            var maxLength = maxLineLen - longlineA.Length - allow.Length + eolLen;

            while (longlineA.Length < maxLength)
            {
                longlineA.Append("a");
                longlineB.Append("a");
            }

            robotsTxt.Append(allow).Append(longlineA).Append("/qux\n");
            robotsTxt.Append(allow).Append(longlineB).Append("/qux\n");

            // URL matches the disallow rule.
            Assert.False(IsUserAgentAllowed(robotsTxt.ToString(), "FooBot", "http://foo.bar/"));
            // Matches the allow rule exactly.
            Assert.True(
                IsUserAgentAllowed(robotsTxt.ToString(), "FooBot",
                    "http://foo.bar" + longlineA + "/qux"));
            // Matches cut off allow rule.
            var actual = IsUserAgentAllowed(robotsTxt.ToString(), "FooBot",
                "http://foo.bar" + longlineB + "/fux");
            Assert.True(actual);
        }

        // Test documentation from
        // https://developers.google.com/search/reference/robots_txt
        // Section "URL matching based on path values".

        [Fact]
        public void GoogleOnly_DocumentationChecks_1()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /fish\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish/salmon.html"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fishheads"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fishheads/yummy.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish.html?id=anything"));

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/Fish.asp"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/catfish"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/?id=fish"));
        }

        // "/fish*" equals "/fish"
        [Fact]
        public void GoogleOnly_DocumentationChecks_2()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /fish*\n";
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish/salmon.html"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fishheads"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fishheads/yummy.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish.html?id=anything"));

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/Fish.bar"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/catfish"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/?id=fish"));
        }

        // "/fish/" does not equal "/fish"
        [Fact]
        public void GoogleOnly_DocumentationChecks_3()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /fish/\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish/"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish/salmon"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish/?salmon"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish/salmon.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/fish/?id=anything"));

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish.html"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/Fish/Salmon.html"));
        }

        // "/*.php"
        [Fact]
        public void GoogleOnly_DocumentationChecks_4()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /*.php\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/filename.php"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/folder/filename.php"));
            Assert.True(IsUserAgentAllowed(
                robotsTxt, "FooBot", "http://foo.bar/folder/filename.php?parameters"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar//folder/any.php.file.html"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/filename.php/"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/index?f=filename.php/"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/php/"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/index?php"));

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/windows.PHP"));
        }

        // "/*.php$"
        [Fact]
        public void GoogleOnly_DocumentationChecks_5()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /*.php$\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/filename.php"));
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/folder/filename.php"));

            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/filename.php?parameters"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/filename.php/"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/filename.php5"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/php/"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/filename?php"));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot",
                "http://foo.bar/aaaphpaaa"));
            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar//windows.PHP"));
        }

        // "/fish*.php"
        [Fact]
        public void GoogleOnly_DocumentationChecks_6()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /fish*.php\n";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/bar"));

            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/fish.php"));
            Assert.True(
                IsUserAgentAllowed(robotsTxt, "FooBot",
                    "http://foo.bar/fishheads/catfish.php?parameters"));

            Assert.False(
                IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/Fish.PHP"));
        }

        // Section "Order of precedence for group-member records".
        [Fact]
        public void GoogleOnly_DocumentationChecks_7()
        {
            var robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /p\n" +
                "disallow: /\n";
            var url = "http://example.com/page";
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /folder\n" +
                "disallow: /folder\n";
            url = "http://example.com/folder/page";
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /page\n" +
                "disallow: /*.htm\n";
            url = "http://example.com/page.htm";
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", url));

            robotsTxt =
                "user-agent: FooBot\n" +
                "allow: /$\n" +
                "disallow: /\n";
            url = "http://example.com/";
            const string urlPage = "http://example.com/page.html";
            Assert.True(IsUserAgentAllowed(robotsTxt, "FooBot", url));
            Assert.False(IsUserAgentAllowed(robotsTxt, "FooBot", urlPage));
        }

        class RobotsStatsReporter : IRobotsParseHandler
        {
            public int LastLineSeen { get; private set; }
            public int ValidDirectives { get; private set; }
            public int UnknownDirectives { get; private set; }
            public string Sitemap { get; private set; } = "";

            public void HandleRobotsStart()
            {
                LastLineSeen = 0;
                ValidDirectives = 0;
                UnknownDirectives = 0;
                Sitemap = "";
            }

            public void HandleRobotsEnd()
            {
            }

            public void HandleUserAgent(int lineNum, ReadOnlySpan<byte> value)
            {
                Digest(lineNum);
            }

            public void HandleAllow(int lineNum, ReadOnlySpan<byte> value)
            {
                Digest(lineNum);
            }

            public void HandleDisallow(int lineNum, ReadOnlySpan<byte> value)
            {
                Digest(lineNum);
            }

            public void HandleSitemap(int lineNum, ReadOnlySpan<byte> value)
            {
                Digest(lineNum);
                Sitemap = Encoding.UTF8.GetString(value.ToArray());
            }

            public void HandleUnknownAction(int lineNum, ReadOnlySpan<byte> action, ReadOnlySpan<byte> value)
            {
                LastLineSeen = lineNum;
                UnknownDirectives++;
            }

            private void Digest(int lineNum)
            {
                Debug.Assert(lineNum > LastLineSeen);
                LastLineSeen = lineNum;
                ValidDirectives++;
            }
        }

        // Different kinds of line endings are all supported: %x0D / %x0A / %x0D.0A
        [Theory]
        [InlineData(
            "User-Agent: foo\n" +
            "Allow: /some/path\n" +
            "User-Agent: bar\n" +
            "\n" +
            "\n" +
            "Disallow: /\n",
            4,
            6
        )]
        [InlineData(
            "User-Agent: foo\r\n" +
            "Allow: /some/path\r\n" +
            "User-Agent: bar\r\n" +
            "\r\n" +
            "\r\n" +
            "Disallow: /\r\n",
            4,
            6
        )]
        [InlineData(
            "User-Agent: foo\r" +
            "Allow: /some/path\r" +
            "User-Agent: bar\r" +
            "\r" +
            "\r" +
            "Disallow: /\r",
            4,
            6
        )]
        [InlineData(
            "User-Agent: foo\n" +
            "Allow: /some/path\n" +
            "User-Agent: bar\n" +
            "\n" +
            "\n" +
            "Disallow: /",
            4,
            6
        )]
        [InlineData(
            "User-Agent: foo\n" +
            "Allow: /some/path\r\n" +
            "User-Agent: bar\n" +
            "\t\n" +
            "\n" +
            "Disallow: /",
            4,
            6
        )]
        public void ID_LinesNumbersAreCountedCorrectly(string file, int expectedValidDirective,
            int expectedLastLineSeen)
        {
            var report = new RobotsStatsReporter();
            RobotsMatcher.ParseRobotsTxt(Encoding.UTF8.GetBytes(file), report);
            Assert.Equal(expectedValidDirective, report.ValidDirectives);
            Assert.Equal(expectedLastLineSeen, report.LastLineSeen);
        }

        // BOM characters are unparseable and thus skipped. The rules following the line
        // are used.
        // We allow as well partial ByteOrderMarks.
        // If the BOM is not the right sequence, the first line looks like garbage
        // that is skipped (we essentially see "\x11\xBFUser-Agent").
        // Some other messed up file: BOMs only valid in the beginning of the file.
        [Theory]
        [InlineData(
            3,
            2,
            0)]
        [InlineData(
            2,
            2,
            0)]
        [InlineData(
            1,
            2,
            0)]
        public void ID_UTF8ByteOrderMarkIsSkipped(int bomLength, int expectedValidDirective,
            int expectedUnkmownDirectives)
        {
            byte[] bom = { 0xEF, 0xBB, 0xBF };

            var rest = Encoding.UTF8.GetBytes("User-Agent: foo\n" +
                                              "Allow: /AnyValue\n");
            var ms = new MemoryStream();
            ms.Write(bom, 0, bomLength);
            ms.Write(rest, 0, rest.Length);

            var report = new RobotsStatsReporter();
            RobotsMatcher.ParseRobotsTxt(ms.GetBuffer(), report);
            Assert.Equal(expectedValidDirective, report.ValidDirectives);
            Assert.Equal(expectedUnkmownDirectives, report.UnknownDirectives);
        }

        [Fact]
        public void ID_Utf8FileBrokenBOM()
        {
            byte[] brokenBom = { 0xEF, 0x11, 0xBF };
            var rest = Encoding.UTF8.GetBytes("User-Agent: foo\n" +
                                              "Allow: /AnyValue\n");
            var ms = new MemoryStream();
            ms.Write(brokenBom, 0, brokenBom.Length);
            ms.Write(rest, 0, rest.Length);

            var report = new RobotsStatsReporter();
            RobotsMatcher.ParseRobotsTxt(ms.GetBuffer(), report);
            Assert.Equal(1, report.ValidDirectives);
            Assert.Equal(1, report.UnknownDirectives);
        }

        [Fact]
        public void ID_Utf8BOMSomewhereInMiddleOfFile()
        {
            var part1 = Encoding.UTF8.GetBytes("User-Agent: foo\n");
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            var part2 = Encoding.UTF8.GetBytes("Allow: /AnyValue\n");
            
            var ms = new MemoryStream();
            ms.Write(part1, 0, part1.Length);
            ms.Write(bom, 0, bom.Length);
            ms.Write(part2, 0, part2.Length);

            var report = new RobotsStatsReporter();
            RobotsMatcher.ParseRobotsTxt(ms.GetBuffer(), report);
            Assert.Equal(1, report.ValidDirectives);
            Assert.Equal(1, report.UnknownDirectives);
        }

        // Google specific: the RFC allows any line that crawlers might need, such as
        // sitemaps, which Google supports.
        // See REP RFC section "Other records".
        // https://www.rfc-editor.org/rfc/rfc9309.html#section-2.2.4
        [Theory]
        [InlineData(
            "User-Agent: foo\n" +
            "Allow: /some/path\n" +
            "User-Agent: bar\n" +
            "\n" +
            "\n" +
            "Sitemap: http://foo.bar/sitemap.xml"
        )]
        [InlineData(
            "Sitemap: http://foo.bar/sitemap.xml\n" +
            "User-Agent: foo\n" +
            "Allow: /some/path\n" +
            "User-Agent: bar\n" +
            "\n" +
            "\n"
        )]
        public void ID_NonStandardLineExample_Sitemap(string robotstxt)
        {
            var report = new RobotsStatsReporter();
            RobotsMatcher.ParseRobotsTxt(Encoding.UTF8.GetBytes(robotstxt), report);
            Assert.Equal("http://foo.bar/sitemap.xml", report.Sitemap);
        }

        [Theory]
        [InlineData("", "/")]
        [InlineData("http://www.example.com", "/")]
        [InlineData("http://www.example.com/", "/")]
        [InlineData("http://www.example.com/a", "/a")]
        [InlineData("http://www.example.com/a/", "/a/")]
        [InlineData("http://www.example.com/a/b?c=http://d.e/", "/a/b?c=http://d.e/")]
        [InlineData("http://www.example.com/a/b?c=d&e=f#fragment", "/a/b?c=d&e=f")]
        [InlineData("example.com", "/")]
        [InlineData("example.com/", "/")]
        [InlineData("example.com/a", "/a")]
        [InlineData("example.com/a/", "/a/")]
        [InlineData("example.com/a/b?c=d&e=f#fragment", "/a/b?c=d&e=f")]
        [InlineData("a", "/")]
        [InlineData("a/", "/")]
        [InlineData("/a", "/a")]
        [InlineData("a/b", "/b")]
        [InlineData("example.com?a", "/?a")]
        [InlineData("example.com/a]b#c", "/a]b")]
        [InlineData("//a/b/c", "/b/c")]
        public void TestGetPathParamsQuery(string url, string expected)
        {
            var actual = RobotsMatcher.GetPathParamsQuery(url);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("http://www.example.com", "http://www.example.com")]
        [InlineData("/a/b/c", "/a/b/c")]
        [InlineData("á", "%C3%A1")]
        [InlineData("%aa", "%AA")]
        public void TestMaybeEscapePattern(string url, string expected)
        {
            var actual =
                Encoding.ASCII.GetString(RobotsTxtParser.MaybeEscapePattern(Encoding.UTF8.GetBytes(url)).ToArray());
            Assert.Equal(expected, actual);
        }
    }
}
