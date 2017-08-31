using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using ScrumBot.Messages;

namespace ScrumBot
{
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
                var pattern = new Regex(@"\bhelp\b", RegexOptions.IgnoreCase);
                if (!message.IsDirectReference || !pattern.IsMatch(message.Text)) return false;

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

                        I can also begin asking a team for their report on demand:
                        > Interview the awesome-people team

                        When you're added to a team that has a scheduled report time, I'll directly message 15 minutes before you to ask for your status.
                    ".TrimStartOfEachLine(' '),
                }));
                return true;
            }

            bool IsModifyTeamMember(IncomingMessage message)
            {
                var pattern = new Regex(@"(?<Command>add|remove) @?(?<UserName>\S+) (to|from)( the)? (?<TeamName>\S+)( team)?", RegexOptions.IgnoreCase);
                var match = pattern.Match(message.Text);
                if (!match.Success) return false;

                var command = match.Groups["Command"].Value.ToLower();
                var teamName = match.Groups["TeamName"].Value;
                var userName = match.Groups["UserName"].Value;
                if (StringComparer.OrdinalIgnoreCase.Equals(userName, "me"))
                {
                    userName = message.AuthorName;
                }
                if (command == "add")
                {
                    scribe.Tell(new AddTeamMember
                    {
                        TeamName = teamName,
                        UserName = userName,
                        InResponseTo = message,
                    });
                }
                else
                {
                    scribe.Tell(new RemoveTeamMember
                    {
                        TeamName = teamName,
                        UserName = userName,
                        InResponseTo = message,
                    });
                }

                return true;
            }

            bool IsListTeams(IncomingMessage message)
            {
                var pattern = new Regex(@"list (the )?teams", RegexOptions.IgnoreCase);
                if (!pattern.IsMatch(message.Text)) return false;

                scribe.Tell(new ListTeams
                {
                    InResponseTo = message,
                });
                return true;
            }

            bool IsRequestInterview(IncomingMessage message)
            {
                var pattern = new Regex(@"interview( the)? (?<TeamName>\S+)( team)?", RegexOptions.IgnoreCase);
                var match = pattern.Match(message.Text);
                if (!match.Success) return false;

                var teamName = match.Groups["TeamName"].Value;

                async Task Execute()
                {
                    var teamResponse = await scribe.Ask<Scribe.GetTeamResponse>(new Scribe.GetTeam
                    {
                        TeamName = teamName,
                    });
                    
                    slacker.Tell(message.Respond(new OutgoingMessage
                    {
                        Text = $"... {teamName} has {teamResponse.Members.Count} members",
                    }));
                }

                Execute().PipeTo(Self);

                return true;
            }

            bool IsInterviewResponse(IncomingMessage message)
            {
                switch (message)
                {
                    case IncomingDirectMessage directMessage:
                        return true;
                        break;
                    default:
                        return false;
                }
            }

            var messageHandlers = new Func<IncomingMessage, bool>[] {
                IsHelp,
                IsModifyTeamMember,
                IsListTeams,
                IsRequestInterview,
            };

            Receive<IncomingMessage>(message =>
            {
                messageHandlers.FirstOrDefault(handler => handler(message));
            });

            Receive<Scribe.AddedTeamMember>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"Added {msg.UserName} to the {msg.TeamName} team!",
                }));
            });

            Receive<Scribe.UserAlreadyTeamMember>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"Looks like {msg.UserName} is already part of the {msg.TeamName} team.",
                }));
            });

            Receive<Scribe.UserNotInTeam>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"Looks like {msg.UserName} isn't in the {msg.TeamName} team.",
                }));
            });

            Receive<Scribe.RemovedUserFromTeam>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"Removed {msg.UserName} from the {msg.TeamName} team.",
                }));
            });

            Receive<Scribe.RemovedTeam>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"Since {msg.UserName} was the last person in the {msg.TeamName} team, I removed the team.",
                }));
            });

            Receive<Scribe.TeamNotFound>(msg =>
            {
                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = $"The {msg.TeamName} team doesn't exist, so I can't remove {msg.UserName} from it.",
                }));
            });

            Receive<Scribe.TeamList>(msg =>
            {
                if (msg.Teams.Any())
                {
                    var teamList = string.Join("\n\n", msg.Teams
                        .OrderBy(x => x.Key)
                        .Select(x => $@"
                                *{x.Key}*
                                > {string.Join("\n> ", x.Value.OrderBy(u => u))}
                            ".TrimStartOfEachLine(' ')));

                    slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                    {
                        Text = teamList,
                    }));
                }
                else
                {
                    slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                    {
                        Text = @"I don't know of any teams. Tell me who should be on which teams.",
                    }));
                }
            });
        }
    }
}
