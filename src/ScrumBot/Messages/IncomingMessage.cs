using SlackConnector.Models;

namespace ScrumBot.Messages
{
    public class IncomingMessage
    {
        public IncomingMessage(SlackMessage message)
        {
            IsDirectMessage = message.ChatHub.Type == SlackChatHubType.DM;
            MentionsBot = message.MentionsBot;
            Text = message.Text;
            AuthorId = message.User.Id;
            AuthorName = message.User.Name;
            AuthorFirstName = message.User.FirstName;
            AuthorLastName = message.User.LastName;
        }

        public bool IsDirectMessage { get; set; }
        public bool MentionsBot { get; set; }
        public bool IsDirectReference => IsDirectMessage || MentionsBot;
        public string Text { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorFirstName { get; set; }
        public string AuthorLastName { get; set; }
    }
}
