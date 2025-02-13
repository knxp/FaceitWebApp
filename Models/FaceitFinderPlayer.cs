namespace faceitWebApp.Models
{
    public class FaceitFinderPlayer
    {
        public string PlayerId { get; set; }
        public string Nickname { get; set; }
        public string Avatar { get; set; }
        public string Country { get; set; }
        public FaceitFinderGames Games { get; set; }
    }

    public class FaceitFinderGames
    {
        public FaceitFinderCS2 CS2 { get; set; }
    }

    public class FaceitFinderCS2
    {
        public int SkillLevel { get; set; }
        public int FaceitElo { get; set; }
    }
}
