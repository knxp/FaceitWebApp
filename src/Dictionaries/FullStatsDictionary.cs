using System.Collections.Generic;

namespace faceitWebApp.Dictionaries
{
    public static class FullStatsDictionary
    {
        public static readonly Dictionary<string, double> Stats = new Dictionary<string, double>
        {
            // Core Combat Stats
            { "Kills", 0 },
            { "Assists", 0 },
            { "Deaths", 0 },
            { "ADR", 0 },
            { "K/D Ratio", 0 },
            { "K/R Ratio", 0 },
            { "Damage", 0 },

            // Headshot Stats
            { "Headshots", 0 },
            { "Headshots %", 0 },

            // Multi-kill Stats
            { "Double Kills", 0 },
            { "Triple Kills", 0 },
            { "Quadro Kills", 0 },
            { "Penta Kills", 0 },

            // Flash Stats
            { "Flash Count", 0 },
            { "Enemies Flashed", 0 },
            { "Flash Successes", 0 },
            { "Flashes per Round in a Match", 0 },
            { "Enemies Flashed per Round in a Match", 0 },
            { "Flash Success Rate per Match", 0 },

            // Utility Stats
            { "Utility Count", 0 },
            { "Utility Damage", 0 },
            { "Utility Successes", 0 },
            { "Utility Usage per Round", 0 },
            { "Utility Success Rate per Match", 0 },
            { "Utility Damage per Round in a Match", 0 },
            { "Utility Damage Success Rate per Match", 0 },
            { "Utility Enemies", 0 },

            // Clutch Stats
            { "Clutch Kills", 0 },
            { "1v1Count", 0 },
            { "1v1Wins", 0 },
            { "Match 1v1 Win Rate", 0 },
            { "1v2Count", 0 },
            { "1v2Wins", 0 },
            { "Match 1v2 Win Rate", 0 },

            // Weapon Stats
            { "Pistol Kills", 0 },
            { "Sniper Kills", 0 },
            { "Sniper Kill Rate per Round", 0 },
            { "Sniper Kill Rate per Match", 0 },
            { "Knife Kills", 0 },
            { "Zeus Kills", 0 },

            // Entry Stats
            { "First Kills", 0 },
            { "Entry Count", 0 },
            { "Entry Wins", 0 },
            { "Match Entry Success Rate", 0 },
            { "Match Entry Rate", 0 },

            // Other Stats
            { "MVPs", 0 },
            { "Result", 0 }
        };

        public static readonly List<string> CustomOrder = new List<string>(Stats.Keys);
    }
}