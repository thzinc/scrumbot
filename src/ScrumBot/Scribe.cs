using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using ExpressMapper;
using ExpressMapper.Extensions;
using ScrumBot.Messages;

namespace ScrumBot
{
    public class Scribe : ReceiveActor // TODO: Make this a persistent actor
    {
        public Scribe(IActorRef slacker)
        {
            Become(() => Listening(slacker));
        }

        public class AddedTeamMember : ModifyTeam { }
        public class UserAlreadyTeamMember : ModifyTeam { }
        public class UserNotInTeam : ModifyTeam { }
        public class RemovedUserFromTeam : ModifyTeam { }
        public class RemovedTeam : ModifyTeam { }
        public class TeamNotFound : ModifyTeam { }

        public class GetTeam
        {
            public string TeamName { get; set; }
        }

        public class GetTeamResponse
        {
            public HashSet<string> Members { get; set; }
        }

        public class TeamList
        {
            public Dictionary<string, HashSet<string>> Teams { get; set; }
            public IncomingMessage InResponseTo { get; set; }
        }

        private void Listening(IActorRef slacker)
        {
            var teams = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            Receive<AddTeamMember>(add =>
            {
                async Task<ModifyTeam> Execute()
                {
                    var userLookup = await slacker.Ask<UserLookupResponse>(new UserLookup { NameOrId = add.UserName }, TimeSpan.FromSeconds(5));
                    var members = teams.GetValueOrDefault(add.TeamName) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!members.Add(userLookup.Name))
                    {
                        return add.Map(new UserAlreadyTeamMember());
                    }

                    teams[add.TeamName] = members;
                    return add.Map(new AddedTeamMember());
                }

                Execute().PipeTo(Sender);
            });

            Receive<RemoveTeamMember>(remove =>
            {
                async Task<ModifyTeam> Execute()
                {
                    var userLookup = await slacker.Ask<UserLookupResponse>(new UserLookup { NameOrId = remove.UserName }, TimeSpan.FromSeconds(5));
                    if (!teams.TryGetValue(remove.TeamName, out var members))
                    {
                        return remove.Map(new TeamNotFound());
                    }

                    if (!members.Remove(userLookup.Name))
                    {
                        return remove.Map(new UserNotInTeam());
                    }

                    if (members.Any())
                    {
                        return remove.Map(new RemovedUserFromTeam());
                    }

                    teams.Remove(remove.TeamName);
                    return remove.Map(new RemovedTeam());
                }

                Execute().PipeTo(Sender);
            });

            Receive<ListTeams>(msg =>
            {
                Sender.Tell(msg.Map(new TeamList
                {
                    Teams = teams,
                }));
            });

            Receive<GetTeam>(msg =>
            {
                if (!teams.TryGetValue(msg.TeamName, out var members))
                {
                    Sender.Tell(msg.Map(new TeamNotFound()));
                }

                Sender.Tell(new GetTeamResponse
                {
                    Members = members,
                });
            });
        }
    }
}
