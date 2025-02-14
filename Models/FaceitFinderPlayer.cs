namespace faceitWebApp.Models
{
    public class FaceitFinderPlayer
    {
        public string PlayerId { get; set; }
        public string Nickname { get; set; }
        public string Avatar { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public int SkillLevel { get; set; }
        public string ProfileURL { get; set; }
        public int FaceitElo { get; set; }
        public bool HasEseaMembership { get; set; }
        public string TeamName { get; set; }
        public string TeamId { get; set; }
        public string TeamDivision { get; set; }
        public DateTime? LatestMatchDate { get; set; }
    }
}
