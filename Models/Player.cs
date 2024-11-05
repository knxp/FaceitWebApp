namespace faceitWebApp.Models
{
    public class Player
    {
        // Basic Info
        public string Nickname { get; set; }
        public string Id { get; set; }
        public string Avatar { get; set; }
        public int? Elo { get; set; }
        public int? Level { get; set; }
        public string Region { get; set; }
        public int? regionRank { get; set; }
        public List<TeamInfo> Teams { get; set; } = new List<TeamInfo>();
        
        // Core Combat Stats
        public double Kills { get; set; }
        public double Assists { get; set; }
        public double Deaths { get; set; }
        public double ADR { get; set; }
        public double KDRatio { get; set; }
        public double KRRRatio { get; set; }

        // Headshot Stats
        public double Headshots { get; set; }
        public double HeadshotsPercentage { get; set; }

        // Multi-kill Stats
        public double DoubleKills { get; set; }
        public double TripleKills { get; set; }
        public double QuadroKills { get; set; }
        public double PentaKills { get; set; }

        // Flash Stats
        public double FlashCount { get; set; }
        public double EnemiesFlashed { get; set; }
        public double FlashSuccesses { get; set; }
        public double FlashesPerRound { get; set; }
        public double EnemiesFlashedPerRound { get; set; }
        public double FlashSuccessRatePerMatch { get; set; }

        // Utility Stats
        public double UtilityUsagePerRound { get; set; }
        public double UtilityCount { get; set; }
        public double UtilityDamage { get; set; }
        public double UtilitySuccesses { get; set; }
        public double UtilitySuccessRatePerMatch { get; set; }
        public double UtilityDamagePerRound { get; set; }
        public double UtilityDamageSuccessRatePerMatch { get; set; }
        public double UtilityEnemies { get; set; }

        // Clutch Stats
        public double ClutchKills { get; set; }
        public double OneVOneCount { get; set; }
        public double OneVOneWins { get; set; }
        public double MatchOneVOneWinRate { get; set; }
        public double OneVTwoCount { get; set; }
        public double OneVTwoWins { get; set; }
        public double MatchOneVTwoWinRate { get; set; }

        // Weapon Stats
        public double PistolKills { get; set; }
        public double SniperKills { get; set; }
        public double SniperKillRatePerRound { get; set; }
        public double SniperKillRatePerMatch { get; set; }
        public double KnifeKills { get; set; }
        public double ZeusKills { get; set; }

        // Entry Stats
        public double FirstKills { get; set; }
        public double EntryCount { get; set; }
        public double EntryWins { get; set; }
        public double MatchEntrySuccessRate { get; set; }
        public double MatchEntryRate { get; set; }

        // Other Stats
        public double Damage { get; set; }
        public double MVPs { get; set; }
        public double Result { get; set; }
    }
}
