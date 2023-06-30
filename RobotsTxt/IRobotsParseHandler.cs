namespace RobotsTxt
{
    public interface IRobotsParseHandler
    {
        void HandleRobotsStart();
        void HandleRobotsEnd();

        void HandleUserAgent(int lineNum, ReadOnlySpan<byte> value);
        void HandleAllow(int lineNum, ReadOnlySpan<byte> value);
        void HandleDisallow(int lineNum, ReadOnlySpan<byte> value);
        void HandleSitemap(int lineNum, ReadOnlySpan<byte> value);
        void HandleUnknownAction(int lineNum, ReadOnlySpan<byte> action, ReadOnlySpan<byte> value);
    }
}