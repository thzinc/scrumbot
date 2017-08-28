using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using ScrumBot.Messages;

namespace ScrumBot
{
    public class Scribe : ReceiveActor // TODO: Make this a persistent actor
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
                async Task Execute()
                {
                    var userLookup = await slacker.Ask<UserLookupResponse>(new UserLookup { NameOrId = add.UserName }, TimeSpan.FromSeconds(5));
                    var members = teams.GetValueOrDefault(add.TeamName) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (members.Add(userLookup.Name))
                    {
                        slacker.Tell(add.InResponseTo.Respond(new OutgoingMessage
                        {
                            Text = $"Added {userLookup.Name} to the {add.TeamName} team!",
                        }));
                    }
                    else
                    {
                        slacker.Tell(add.InResponseTo.Respond(new OutgoingMessage
                        {
                            Text = $"Looks like {userLookup.Name} is already part of the {add.TeamName} team.",
                        }));
                    }
                    teams[add.TeamName] = members;
                }

                Execute().PipeTo(Self);
            });

            Receive<RemoveTeamMember>(remove =>
            {
                async Task Execute()
                {
                    var userLookup = await slacker.Ask<UserLookupResponse>(new UserLookup { NameOrId = remove.UserName }, TimeSpan.FromSeconds(5));
                    if (teams.TryGetValue(remove.TeamName, out var members))
                    {
                        if (!members.Remove(userLookup.Name))
                        {
                            slacker.Tell(remove.InResponseTo.Respond(new OutgoingMessage
                            {
                                Text = $"Looks like {userLookup.Name} isn't in the {remove.TeamName} team",
                            }));
                        }
                        else if (members.Any())
                        {
                            slacker.Tell(remove.InResponseTo.Respond(new OutgoingMessage
                            {
                                Text = $"Removed {userLookup.Name} from the {remove.TeamName} team",
                            }));
                        }
                        else
                        {
                            teams.Remove(remove.TeamName);
                            slacker.Tell(remove.InResponseTo.Respond(new OutgoingMessage
                            {
                                Text = $"Since {userLookup.Name} was the last person in the {remove.TeamName} team, I removed the team.",
                            }));
                        }
                    }
                    else
                    {
                        slacker.Tell(remove.InResponseTo.Respond(new OutgoingMessage
                        {
                            Text = $"The {remove.TeamName} team doesn't exist, so I can't remove {userLookup.Name} from it.",
                        }));
                    }
                }

                Execute().PipeTo(Self);
            });

            Receive<ListTeams>(msg =>
            {
                var teamList = string.Join("\n\n", teams
                    .OrderBy(x => x.Key)
                    .Select(x => $@"
                            *{x.Key}*
                            > {string.Join("\n> ", x.Value.OrderBy(u => u))}
                        ".TrimStartOfEachLine(' ')));

                slacker.Tell(msg.InResponseTo.Respond(new OutgoingMessage
                {
                    Text = teamList,
                }));
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

            bool IsModifyTeamMember(IncomingMessage message)
            {
                var pattern = new Regex(@"(?<Command>add|remove) @?(?<UserName>\S+) (to|from)( the)? (?<TeamName>\S+)( team)?", RegexOptions.IgnoreCase);
                var match = pattern.Match(message.Text);
                if (match.Success)
                {
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

                return false;
            }

            bool IsListTeams(IncomingMessage message)
            {
                var pattern = new Regex(@"list (the )?teams", RegexOptions.IgnoreCase);
                if (pattern.IsMatch(message.Text))
                {
                    scribe.Tell(new ListTeams
                    {
                        InResponseTo = message,
                    });
                    return true;
                }

                return false;
            }

            var messageHandlers = new Func<IncomingMessage, bool>[] {
                IsHelp,
                IsModifyTeamMember,
                IsListTeams,
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
