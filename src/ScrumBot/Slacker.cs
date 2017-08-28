using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using ScrumBot.Configuration;
using ScrumBot.Messages;
using SlackConnector;
using SlackConnector.Models;

namespace ScrumBot
{
    public class Slacker : ReceiveActor, IWithUnboundedStash
    {
        private readonly IActorRef _messageTarget;

        public IStash Stash { get; set; }

        public Slacker(IActorRef messageTarget)
        {
            Become(WaitingForConnection);
            _messageTarget = messageTarget;
        }

        public void WaitingForConnection()
        {
            Receive<ISlackConnection>(connection =>
            {
                Become(() => Connected(connection));

                Stash.UnstashAll();
            });

            ReceiveAny(_ => Stash.Stash());

            var slackConfiguration = new Slack();
            var connector = new SlackConnector.SlackConnector();
            connector.Connect(slackConfiguration.Token).PipeTo(Self);
        }

        private void Connected(ISlackConnection connection)
        {
            var cannotRespondHere = new HashSet<string>();
            Receive<SlackMessage>(message =>
            {
                switch (message.ChatHub.Type)
                {
                    case SlackChatHubType.Channel:
                        _messageTarget.Tell(new IncomingChannelMessage(message));
                        break;
                    case SlackChatHubType.DM:
                        _messageTarget.Tell(new IncomingDirectMessage(message));
                        break;
                    default:
                        if (cannotRespondHere.Add(message.ChatHub.Id))
                        {
                            connection
                                .Say(new BotMessage
                                {
                                    ChatHub = message.ChatHub,
                                    Text = $"I don't know how to respond in this chat. Send me a direct message or speak to me in a channel.",
                                })
                                .PipeTo(Self);
                        }

                        break;
                }
                Console.WriteLine($"Received {message.Text} from {message.User.Id} ({message.User.Name})");
            });

            Receive<OutgoingChannelMessage>(message =>
            {
                connection
                    .Say(new BotMessage
                    {
                        ChatHub = connection.ConnectedChannel(message.ChannelName),
                        Text = message.Text,
                    })
                    .PipeTo(Self);
            });

            Receive<OutgoingDirectMessage>(message =>
            {
                async Task Execute()
                {
                    var chatHub = connection.ConnectedDM(message.UserName);
                    await connection.Say(new BotMessage
                    {
                        ChatHub = chatHub,
                        Text = message.Text,
                    });
                }

                Execute().PipeTo(Self);
            });

            var self = Self;

            connection.OnMessageReceived += message =>
            {
                self.Tell(message);
                return Task.CompletedTask;
            };
        }
    }
}
