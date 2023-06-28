using System;
using System.Collections.Generic;
using System.IO;
using RobotsTxt;

namespace ConsoleApplication
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            var filename = args.Length > 0 ? args[0] : "";
            if (filename == "-h" || filename == "-help" || filename == "--help")
            {
                ShowHelp();
                return 0;
            }

            if (args.Length != 3)
            {
                Console.Error.WriteLine("Invalid amount of arguments. Showing help.");
                ShowHelp();
                return 0;
            }

            var robotsContent = File.ReadAllBytes(filename);
            var userAgent = args[1];
            var userAgents = new List<string> { userAgent };

            var matcher = new RobotsMatcher();
            var url = args[2];

            var allowed = matcher.AllowedByRobots(robotsContent, userAgents, url);
            var allowedString = allowed ? "ALLOWED" : "DISALLOWED";

            Console.WriteLine($"User-Agent '{userAgent}' with URL '{url}': {allowedString}");
            if (robotsContent.Length == 0)
            {
                Console.WriteLine("Notice: robots file is empty so all user-agents are allowed");
            }

            return allowed ? 0 : 1;
        }

        private static void ShowHelp()
        {
            var appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            Console.WriteLine("Shows whether the given user_agent and URI combination" +
                              " is allowed or disallowed by the given robots.txt file. ");
            Console.WriteLine("Usage: ");
            Console.WriteLine($"{appName} <robots.txt filename> <user_agent> <URI>\n");
            Console.WriteLine("The URI must be %-encoded according to RFC3986.\n");
            Console.WriteLine("Example:\n" +
                              $"  {appName} robots.txt FooBot http://example.com/foo");
        }
    }
}