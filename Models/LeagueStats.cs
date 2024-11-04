using System.Collections.Generic;

namespace faceitApp.Models
{
    public class LeagueStats
    {
        public string Season { get; set; }
        public string Division { get; set; }
        public string Location { get; set; }
        public string DivisionLocation { get; set; }
        public string GameType { get; set; }
        public Player Stats { get; set; }
        public int MatchCount { get; set; }
    }

    public class LeagueStatsCollection
    {
        public List<LeagueStats> SeasonStats { get; set; } = new List<LeagueStats>();
        public Player OverallStats { get; set; }
        public int TotalMatches { get; set; }
    }
}