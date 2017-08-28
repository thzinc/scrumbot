namespace ScrumBot.Messages
{
    public abstract class ModifyTeam
    {
        public string TeamName { get; set; }
        public string UserName { get; set; }
        public IncomingMessage InResponseTo { get; set; }
    }
}
