using SlackConnector.Models;

namespace ScrumBot.Messages
{
    public class IncomingChannelMessage : IncomingMessage
    {
        public IncomingChannelMessage(SlackMessage message)
            : base(message)
        {
            ChannelName = message.ChatHub.Name;
        }

        public string ChannelName { get; set; }
    }
}
