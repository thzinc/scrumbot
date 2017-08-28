using System;
using Akka.Actor;
using ScrumBot.Messages;

namespace ScrumBot
{
    public class ScrumBot : ReceiveActor
    {
        public ScrumBot()
        {
            var self = Self;
            var slacker = Context.ActorOf(Props.Create(() => new Slacker(self)));
            var executiveAdministrator = Context.ActorOf(Props.Create(() => new ExecutiveAdministrator(slacker)));
            Become(() => Running(executiveAdministrator));
        }

        private void Running(IActorRef executiveAdministrator)
        {
            Receive<IncomingMessage>(incomingMessage =>
            {
                executiveAdministrator.Tell(incomingMessage);
            });

            Receive<string>(s => Console.WriteLine(s));

            Self.Tell("ScrumBot now running");
        }
    }
}
