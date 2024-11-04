using System.Collections.Generic;

namespace faceitApp.Models
{
    public class TeamInfo
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
        public List<TeamPlayer> Players { get; set; } = new List<TeamPlayer>();
        public string GameId { get; set; }
    }

    public class TeamPlayer
    {
        public string Nickname { get; set; }
        public string PlayerId { get; set; }
        public string Avatar { get; set; }
        public int? Elo { get; set; }
    }
}