using System.Text;
using RobotsTxt;
using Xunit;

namespace TestRobotsTxt
{
    public class TestRobotsTxtParser
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("a", "a")]
        [InlineData("abc", "abc")]
        [InlineData(" ", "")]
        [InlineData(" a", "a")]
        [InlineData(" abc", "abc")]
        [InlineData("a ", "a")]
        [InlineData("abc ", "abc")]
        [InlineData("  ", "")]
        [InlineData(" a ", "a")]
        [InlineData(" abc ", "abc")]
        [InlineData("     a ", "a")]
        [InlineData(" abc     ", "abc")]
        public void TestStripWhitespaceSlowly(string s, string expected)
        {
            var actual = RobotsTxtParser.StripWhitespaceSlowly(Encoding.UTF8.GetBytes(s));
            Assert.Equal(expected, Encoding.UTF8.GetString(actual.ToArray()));
        }

        [Theory]
        [InlineData("", false, null, null)]
        [InlineData(":", false, "", null)]
        [InlineData("abc:", true, "abc", "")]
        [InlineData("allow: foo", true, "allow", "foo")]
        [InlineData("allow:foo", true, "allow", "foo")]
        [InlineData("  allow  :    foo  ", true, "allow", "foo")]
        [InlineData("#", false, null, null)]
        [InlineData("  :  #", false, "", null)]
        [InlineData("allow: \t\tfoo\t# Bar", true, "allow", "foo")]
        public void TestGetKeyAndValueFrom(string line, bool rc, string expectedKey, string expectedValue)
        {
            var actual = RobotsTxtParser.GetKeyAndValueFrom(out var key, out var value, Encoding.UTF8.GetBytes(line));
            Assert.Equal(rc, actual);
            var k = key == null ? null : Encoding.UTF8.GetString(key.ToArray());
            Assert.Equal(expectedKey, k);
            var v = value == null ? null : Encoding.UTF8.GetString(value.ToArray());
            Assert.Equal(expectedValue, v);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("abcd", "abcd")]
        [InlineData("%", "%")]
        [InlineData("%a", "%a")]
        [InlineData("%xx", "%xx")]
        [InlineData("%2f", "%2F")]
        [InlineData("é", "%C3%A9")]
        public void TestMaybeEscapePattern(string src, string expected)
        {
            var actual = RobotsTxtParser.MaybeEscapePattern(Encoding.UTF8.GetBytes(src));
            Assert.Equal(expected, Encoding.UTF8.GetString(actual.ToArray()));
        }
    }
}
