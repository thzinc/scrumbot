using System;

namespace ScrumBot.Messages
{
    public static class IncomingMessageExtensions
    {
        public static OutgoingMessage Respond(this IncomingMessage incoming, OutgoingMessage outgoing)
        {
            switch (incoming)
            {
                case IncomingChannelMessage channelMessage:
                    return new OutgoingChannelMessage
                    {
                        ChannelName = channelMessage.ChannelName,
                        Text = outgoing.Text,
                    };
                case IncomingDirectMessage directMessage:
                    return new OutgoingDirectMessage
                    {
                        UserName = directMessage.UserName,
                        Text = outgoing.Text,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(incoming));
            }
        }
    }
}
