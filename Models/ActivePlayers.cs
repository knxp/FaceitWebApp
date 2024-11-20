namespace faceitWebApp.Models
{
    public class ActivePlayer
    {
        public string PlayerId { get; set; }
        public string Nickname { get; set; }
        public string Avatar { get; set; }
        public int? Elo { get; set; }
        public DateTime LastMatchDate { get; set; }
        public int MatchesWithTeam { get; set; }
        public bool IsActive { get; set; }
    }
}