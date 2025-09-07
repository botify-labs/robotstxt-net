using System.Text;

using Xunit;

using RobotsTxt;

namespace TestRobotsTxt
{
    public class TestsLongestMatchRobotsMatchStrategy
    {
        [Theory]
        [InlineData("/", "/", true)]
        [InlineData("/", "/$", true)]
        [InlineData("a", "b", false)]
        [InlineData("abcd", "a", true)]
        [InlineData("abcd", "a$", false)]
        [InlineData("abcd", "a*", true)]
        [InlineData("abcd", "a*b", true)]
        [InlineData("abcd", "a*c", true)]
        [InlineData("abcd", "a*d", true)]
        [InlineData("abcd", "a*d$", true)]
        [InlineData("abcd", "a*c$", false)]
        [InlineData("/abcd/e//fg/hij/k/lm/nop/q/r/", "/*/*/*/*/*/*/*/*/*/*/*", true)]
        public void TestMatch(string path, string pattern, bool expected)
        {
            var actual =
                LongestMatchRobotsMatchStrategy.MatchesSlow(
                    Encoding.UTF8.GetBytes(path),
                    Encoding.UTF8.GetBytes(pattern)
                );
            Assert.Equal(expected, actual);
            var haveWildcards = pattern.Length >= 1 && (pattern.Contains('*') || pattern[^1] == '$');
            actual =
                LongestMatchRobotsMatchStrategy.MatchesFast(
                    Encoding.UTF8.GetBytes(path),
                    Encoding.UTF8.GetBytes(pattern),
                    haveWildcards
                );
            Assert.Equal(expected, actual);
        }
    }
}
