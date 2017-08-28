using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Akka.Actor;
using ScrumBot.Messages;

namespace ScrumBot
{
    public static class StringExtensions
    {
        public static string TrimStartOfEachLine(this string input, char trimChar)
        {
            var lines = input.Split('\n').Select(line => line.TrimStart(trimChar));

            return string.Join('\n', lines);
        }
    }
    public class Scribe : ReceiveActor
    {
        public Scribe(IActorRef slacker)
        {
            Become(() => Listening(slacker));
        }

        private void Listening(IActorRef slacker)
        {
            var teams = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            Receive<AddTeamMember>(add =>
            {
                var members = teams.GetValueOrDefault(add.TeamName) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (members.Add(add.UserName))
                {
                    slacker.Tell(add.InResponseTo.Respond(new OutgoingMessage
                    {
                        Text = $"Added {add.UserName} to the {add.TeamName} team!",
                    }));
                }
                else
                {
                    slacker.Tell(add.InResponseTo.Respond(new OutgoingMessage
                    {
                        Text = $"Looks like {add.UserName} is already part of the {add.TeamName} team.",
                    }));
                }
                teams[add.TeamName] = members;
            });
        }
    }
    public class ExecutiveAdministrator : ReceiveActor
    {
        public ExecutiveAdministrator(IActorRef slacker)
        {
            var scribe = Context.ActorOf(Props.Create(() => new Scribe(slacker)));
            Become(() => Listening(slacker, scribe));
        }

        private void Listening(IActorRef slacker, IActorRef scribe)
        {
            bool IsHelp(IncomingMessage message)
            {
                if (message.IsDirectReference)
                {
                    var pattern = new Regex(@"\bhelp\b", RegexOptions.IgnoreCase);
                    if (pattern.IsMatch(message.Text))
                    {
                        slacker.Tell(message.Respond(new OutgoingMessage
                        {
                            Text = $@"
                                I help with lots of things, {message.AuthorFirstName}!
                                {(!message.IsDirectMessage ? "You can chat with me in a direct message too.\n" : "")}
                                I can add users to teams:
                                > Add @{message.AuthorName} to the awesome-people team
                                
                                I can remove users from teams:
                                > Remove @{message.AuthorName} from the other-people team

                                I can tell you about the teams:
                                > List the teams

                                I can configure team reports to be posted at a particular time throughout the week:
                                > Report the awesome-people team status in #awesome-stat at 10:00 AM

                                When you're added to a team that has a scheduled report time, I'll directly message 15 minutes before you to ask for your status.
                            ".TrimStartOfEachLine(' '),
                        }));
                        return true;
                    }
                }

                return false;
            }

            bool IsAddTeamMember(IncomingMessage message)
            {
                var pattern = new Regex(@"add @?(?<UserName>\S+) to( the)? (?<TeamName>\S+)( team)?", RegexOptions.IgnoreCase);
                var match = pattern.Match(message.Text);
                if (match.Success)
                {
                    var teamName = match.Groups["TeamName"].Value;
                    var userName = match.Groups["UserName"].Value;
                    if (StringComparer.OrdinalIgnoreCase.Equals(userName, "me"))
                    {
                        userName = message.AuthorName;
                    }
                    scribe.Tell(new AddTeamMember
                    {
                        TeamName = teamName,
                        UserName = userName,
                        InResponseTo = message,
                    });
                    return true;
                }

                return false;
            }

            var messageHandlers = new Func<IncomingMessage, bool>[] {
                IsHelp,
                IsAddTeamMember,
            };

            Receive<IncomingMessage>(message =>
            {
                // TODO: Determine what's said and how to respond
                /*
                    @ScrumBot help
                    Help @ScrumBot

                    Add @user to the blank team
                    Add @user to blank

                    Remove @user from the blank team
                    Remove @user from blank

                    Report the blank team status in #channel at 10:00 AM
                    Report blank in #channel at 10:00 AM
                    Give a report of the blank team in #channel at 10:00 AM
                * */

                messageHandlers.FirstOrDefault(handler => handler(message));
            });
        }
    }
}
