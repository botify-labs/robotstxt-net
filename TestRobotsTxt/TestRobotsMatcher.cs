using System.Text;
using RobotsTxt;
using Xunit;

namespace TestRobotsTxt
{
    public class TestRobotsMatcher
    {
        [Theory]
        [InlineData("Googlebot", "Googlebot")]
        [InlineData("Googlebot/1.0", "Googlebot")]
        public void TestExtractUserAgent(string userAgent, string expected)
        {
            var actual = RobotsMatcher.ExtractUserAgent(Encoding.UTF8.GetBytes(userAgent));
            Assert.Equal(expected, Encoding.UTF8.GetString(actual.ToArray()));
        }

        [Theory]
        [InlineData("http://example.com", "/")]
        [InlineData("http://example.com/", "/")]
        [InlineData("http://example.com/foo", "/foo")]
        [InlineData("http://example.com/foo#", "/foo")]
        [InlineData("http://example.com/foo?", "/foo?")]
        [InlineData("http://example.com/foo;", "/foo;")]
        public void TestGetPathParamsQuery(string url, string expected)
        {
            var actual = RobotsMatcher.GetPathParamsQuery(url);
            Assert.Equal(expected, actual);
        }
    }
}
