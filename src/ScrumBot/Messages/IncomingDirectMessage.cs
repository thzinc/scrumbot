using SlackConnector.Models;

namespace ScrumBot.Messages
{
    public class IncomingDirectMessage : IncomingMessage
    {
        public IncomingDirectMessage(SlackMessage message)
            : base(message)
        {
            UserName = message.ChatHub.Name;
        }

        public string UserName { get; set; }
    }
}
