using System;

namespace ScrumBot.Configuration
{
    public class Slack
    {
        public string Token { get; set; } = Environment.GetEnvironmentVariable("SLACK_TOKEN");
    }
}
