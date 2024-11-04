namespace faceitWebApp.Dictionaries
{
    public static class TeamStatsDictionary
{
    public static readonly Dictionary<string, MapStats> MapStatsCollection = new Dictionary<string, MapStats>();

    public class TeamStats
    {
        public int Matches { get; set; }
        public int Wins { get; set; }
        public double WinRate { get; set; }
        public int WinStreak { get; set; }
    }

    public class MapStats
    {
        public int Matches { get; set; }
        public int Wins { get; set; }
        public double WinRate { get; set; }
    }
}
}