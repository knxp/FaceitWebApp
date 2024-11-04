using System;

namespace faceitWebApp.Models
{
    public class MapStats
    {
        public string Map { get; set; }
        public int TotalMatches { get; set; }
        public int Wins { get; set; }
        public double WinRate => TotalMatches > 0 ? (Wins * 100.0) / TotalMatches : 0;
    }
}