using System;
using Xunit;
using RobotsTxt;

namespace TestRobotsTxt
{
    public class Tests
    {
        [Fact]
        public void Test1()
        {
            Assert.True(true);
        }

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
            var actual = LongestMatchRobotsMatchStrategy.Matches(path, pattern);
            Assert.Equal(expected, actual);
        }
    }
}