using System.Collections.Generic;

namespace faceitApp.Models
{
    public class TeamStats
    {
        public int TotalMatches { get; set; }
        public int WinCount { get; set; }
        public double WinRate => TotalMatches > 0 ? (WinCount * 100.0) / TotalMatches : 0;
        public int CurrentStreak { get; set; }
        public string LongestWinStreak { get; set; }
        public List<TeamMatchHistory> RecentMatches { get; set; } = new List<TeamMatchHistory>();
    }

    public class TeamMatchHistory
    {
        public string MatchId { get; set; }
        public string TeamId { get; set; }
        public int Result { get; set; }  // 1 for win, 0 for loss
    }
}