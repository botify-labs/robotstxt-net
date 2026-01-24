using System.Text;

using Xunit;

using RobotsTxt;

namespace TestRobotsTxt
{
    public class TestsLongestMatchRobotsMatchStrategy
    {
        [Theory]
        [InlineData("/", "/", true, 1)]
        [InlineData("/", "/$", true, 2)]
        [InlineData("a", "b", false, -1)]
        [InlineData("abcd", "a", true, 1)]
        [InlineData("abcd", "a$", false, -1)]
        [InlineData("abcd", "a*", true, 2)]
        [InlineData("abcd", "a*b", true, 3)]
        [InlineData("abcd", "a*c", true, 3)]
        [InlineData("abcd", "a*d", true, 3)]
        [InlineData("abcd", "a*d$", true, 4)]
        [InlineData("abcd", "a*c$", false, -1)]
        [InlineData("/abcd/e//fg/hij/k/lm/nop/q/r/", "/*/*/*/*/*/*/*/*/*/*/*", true, 22)]
        public void TestMatch(string path, string pattern, bool expected, int len)
        {
            var actual =
                LongestMatchRobotsMatchStrategy.MatchesSlow(
                    Encoding.UTF8.GetBytes(path),
                    Encoding.UTF8.GetBytes(pattern)
                );
            Assert.Equal(expected, actual);
            var haveWildcards = pattern.Length >= 1 && (pattern.Contains('*') || pattern[^1] == '$');
            var actualLen =
                LongestMatchRobotsMatchStrategy.MatchFast(
                    Encoding.UTF8.GetBytes(path),
                    Encoding.UTF8.GetBytes(pattern),
                    haveWildcards
                );
            Assert.Equal(len, actualLen);
        }
    }
}
