namespace faceitWebApp.Models
{
    public class Match
    {
        public string MatchId { get; set; }
        public string Map { get; set; }
        public MatchTeam Team1 { get; set; }
        public MatchTeam Team2 { get; set; }
    }

    public class MatchTeam
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public List<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
    }

    public class MatchPlayer
    {
        public string Nickname { get; set; }
        public string AvatarUrl { get; set; }

        // Core stats
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double KDRatio { get; set; }
        public double ADR { get; set; }

        // Special kills
        public int DoubleKills { get; set; }
        public int PistolKills { get; set; }
        public int SniperKills { get; set; }

        // Entry stats
        public int EntryCount { get; set; }
        public int EntryWins { get; set; }
        public double EntrySuccessRate { get; set; }

        // Utility and flash stats
        public double UtilityDamage { get; set; }
        public int FlashCount { get; set; }

        // Headshot stats
        public int Headshots { get; set; }
        public double HeadshotPercentage { get; set; }

        // Clutch stats
        public int OneVOneWins { get; set; }

        // Damage stats
        public int Damage { get; set; }
    }
}