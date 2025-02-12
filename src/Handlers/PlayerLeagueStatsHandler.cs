using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using faceitWebApp.Models;

namespace faceitWebApp.Handlers
{
    public class PlayerLeagueStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private readonly Regex _eseaRegex = new Regex(
            @"ESEA\s+S(\d+)\s+(\w+)\s+(\w+)\s+(\w+)\s*-\s*(.+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public PlayerLeagueStatsHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["faceitapikey"];
            Console.WriteLine($"Handler API Key present: {!string.IsNullOrEmpty(_faceitApiKey)}"); // Temporary logging
        }

        public async Task<LeagueStatsCollection> GetLeagueStatsAsync(string playerId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);
                Console.WriteLine($"Authorization Header: {_httpClient.DefaultRequestHeaders.Authorization}"); // Temporary logging

                var collection = new LeagueStatsCollection();
                var seasonStats = new Dictionary<string, (LeagueStats Stats, int MatchCount, double TotalRounds)>();
                var overallStats = new Player { Id = playerId };
                var ratingHandler = new RatingHandler();

                int offset = 0;
                const int limit = 100;
                int totalMatches = 0;
                bool hasMoreMatches = true;

                while (hasMoreMatches)
                {
                    var matchHistoryUrl = $"https://open.faceit.com/data/v4/players/{playerId}/history?game=cs2&offset={offset}&limit={limit}";
                    var response = await _httpClient.GetAsync(matchHistoryUrl);

                    if (!response.IsSuccessStatusCode)
                        break;

                    var matchHistory = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var matches = matchHistory["items"] as JArray;

                    if (matches == null || !matches.Any())
                        break;

                    var eseaMatches = matches
                        .Where(m =>
                            m["competition_type"]?.ToString() == "championship" &&
                            m["competition_name"]?.ToString().Contains("ESEA", StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    if (!eseaMatches.Any())
                    {
                        offset += limit;
                        continue;
                    }

                    var matchTasks = eseaMatches.Select(match => ProcessMatchAsync(match, playerId));
                    var results = await Task.WhenAll(matchTasks);

                    foreach (var result in results.Where(r => r.HasValue))
                    {
                        var (matchStats, leagueInfo, totalRounds) = result.Value;
                        totalMatches++;

                        var seasonKey = $"{leagueInfo.Season}-{leagueInfo.Division}-{leagueInfo.Location}";

                        // Update season stats
                        if (!seasonStats.ContainsKey(seasonKey))
                        {
                            seasonStats[seasonKey] = (new LeagueStats
                            {
                                Season = leagueInfo.Season,
                                Division = leagueInfo.Division,
                                Location = leagueInfo.Location,
                                DivisionLocation = leagueInfo.DivisionLocation,
                                GameType = leagueInfo.GameType,
                                Stats = new Player { Id = playerId }
                            }, 0, 0);
                        }

                        UpdateStats(seasonStats[seasonKey].Stats.Stats, matchStats);
                        seasonStats[seasonKey] = (
                            seasonStats[seasonKey].Stats,
                            seasonStats[seasonKey].MatchCount + 1,
                            seasonStats[seasonKey].TotalRounds + totalRounds
                        );
                        UpdateStats(overallStats, matchStats);
                    }

                    offset += limit;
                    hasMoreMatches = matches.Count == limit;
                }

                // Calculate averages and build collection
                foreach (var (_, (stats, matchCount, totalRounds)) in seasonStats)
                {
                    CalculateAverages(stats.Stats, matchCount);
                    stats.MatchCount = matchCount;

                    if (int.TryParse(stats.Season.TrimStart('S'), out int seasonNumber) && seasonNumber >= 51)
                    {
                        stats.Stats.Rating = ratingHandler.CalculateRating(stats.Stats, totalRounds, matchCount);
                    }

                    collection.SeasonStats.Add(stats);
                }

                CalculateAverages(overallStats, totalMatches);
                collection.OverallStats = overallStats;
                collection.TotalMatches = totalMatches;

                return collection;
            }
            catch
            {
                return null;
            }
        }

        private async Task<(Player Stats, LeagueStats LeagueInfo, double Rounds)?> ProcessMatchAsync(JToken match, string playerId)
        {
            try
            {
                var matchId = match["match_id"].ToString();
                var competitionName = match["competition_name"]?.ToString() ?? "";

                var eseaMatch = _eseaRegex.Match(competitionName);
                if (!eseaMatch.Success)
                    return null;

                var seasonNumber = int.Parse(eseaMatch.Groups[1].Value);
                var leagueInfo = new LeagueStats
                {
                    Season = $"S{seasonNumber}",
                    Location = eseaMatch.Groups[2].Value,
                    Division = eseaMatch.Groups[3].Value,
                    DivisionLocation = eseaMatch.Groups[4].Value,
                    GameType = eseaMatch.Groups[5].Value.Trim()
                };

                var matchStatsUrl = $"https://open.faceit.com/data/v4/matches/{matchId}/stats";
                using var response = await _httpClient.GetAsync(matchStatsUrl);

                if (!response.IsSuccessStatusCode)
                    return null;

                var matchStats = JObject.Parse(await response.Content.ReadAsStringAsync());
                var rounds = matchStats["rounds"] as JArray;

                if (rounds?.Any() != true)
                    return null;

                // Get total rounds from round_stats
                double totalRounds = 0;
                var roundStats = rounds[0]["round_stats"];
                if (roundStats != null && roundStats["Rounds"] != null)
                {
                    totalRounds = double.Parse(roundStats["Rounds"].ToString());
                }

                var playerStats = rounds[0]["teams"]
                    .SelectMany(t => t["players"])
                    .FirstOrDefault(p => p["player_id"]?.ToString() == playerId)?["player_stats"] as JObject;

                if (playerStats == null)
                    return null;

                var stats = new Player { TotalRounds = totalRounds };  // Initialize with totalRounds

                // Basic stats for all seasons
                stats.Kills = ParseStat(playerStats["Kills"]);
                stats.Deaths = ParseStat(playerStats["Deaths"]);
                stats.Assists = ParseStat(playerStats["Assists"]);
                stats.KDRatio = ParseStat(playerStats["K/D Ratio"]);
                stats.KRRRatio = ParseStat(playerStats["K/R Ratio"]);
                stats.HeadshotsPercentage = ParseStat(playerStats["Headshots %"]);
                stats.Headshots = ParseStat(playerStats["Headshots"]);
                stats.MVPs = ParseStat(playerStats["MVPs"]);
                stats.TripleKills = ParseStat(playerStats["Triple Kills"]);
                stats.QuadroKills = ParseStat(playerStats["Quadro Kills"]);
                stats.PentaKills = ParseStat(playerStats["Penta Kills"]);

                // Detailed stats only for S51+
                if (seasonNumber >= 51)
                {
                    // Combat Stats
                    stats.ADR = ParseStat(playerStats["ADR"]);
                    stats.Damage = ParseStat(playerStats["Damage"]);

                    // Entry Stats
                    stats.FirstKills = ParseStat(playerStats["First Kills"]);
                    stats.EntryCount = ParseStat(playerStats["Entry Count"]);
                    stats.EntryWins = ParseStat(playerStats["Entry Wins"]);
                    stats.MatchEntrySuccessRate = ParseStat(playerStats["Match Entry Success Rate"]);
                    stats.MatchEntryRate = ParseStat(playerStats["Match Entry Rate"]);

                    // Clutch Stats
                    stats.OneVOneCount = ParseStat(playerStats["1v1Count"]);
                    stats.OneVOneWins = ParseStat(playerStats["1v1Wins"]);
                    stats.OneVTwoCount = ParseStat(playerStats["1v2Count"]);
                    stats.OneVTwoWins = ParseStat(playerStats["1v2Wins"]);
                    stats.ClutchKills = ParseStat(playerStats["Clutch Kills"]);

                    // Weapon Stats
                    stats.PistolKills = ParseStat(playerStats["Pistol Kills"]);
                    stats.SniperKills = ParseStat(playerStats["Sniper Kills"]);
                    stats.KnifeKills = ParseStat(playerStats["Knife Kills"]);
                    stats.ZeusKills = ParseStat(playerStats["Zeus Kills"]);

                    // Flash Stats
                    stats.FlashCount = ParseStat(playerStats["Flash Count"]);
                    stats.EnemiesFlashed = ParseStat(playerStats["Enemies Flashed"]);
                    stats.FlashSuccesses = ParseStat(playerStats["Flash Successes"]);
                    stats.FlashesPerRound = ParseStat(playerStats["Flashes per Round in a Match"]);
                    stats.EnemiesFlashedPerRound = ParseStat(playerStats["Enemies Flashed per Round in a Match"]);
                    stats.FlashSuccessRatePerMatch = ParseStat(playerStats["Flash Success Rate per Match"]);

                    // Utility Stats
                    stats.UtilityCount = ParseStat(playerStats["Utility Count"]);
                    stats.UtilityDamage = ParseStat(playerStats["Utility Damage"]);
                    stats.UtilitySuccesses = ParseStat(playerStats["Utility Successes"]);
                    stats.UtilityUsagePerRound = ParseStat(playerStats["Utility Usage per Round"]);
                    stats.UtilityDamagePerRound = ParseStat(playerStats["Utility Damage per Round in a Match"]);
                    stats.UtilitySuccessRatePerMatch = ParseStat(playerStats["Utility Success Rate per Match"]);
                    stats.UtilityDamageSuccessRatePerMatch = ParseStat(playerStats["Utility Damage Success Rate per Match"]);
                    stats.UtilityEnemies = ParseStat(playerStats["Utility Enemies"]);

                    // Calculate additional rates if not provided directly
                    if (stats.OneVOneCount > 0)
                        stats.MatchOneVOneWinRate = (stats.OneVOneWins / stats.OneVOneCount) * 100;

                    if (stats.OneVTwoCount > 0)
                        stats.MatchOneVTwoWinRate = (stats.OneVTwoWins / stats.OneVTwoCount) * 100;
                }

                return (stats, leagueInfo, totalRounds);
            }
            catch
            {
                return null;
            }
        }


        private static Player ParsePlayerStats(JObject stats)
        {
            static double ParseStat(JToken stat)
            {
                if (stat == null)
                    return 0;

                var value = stat.ToString();
                if (value.EndsWith("%"))
                {
                    if (double.TryParse(value.TrimEnd('%'), out double percentResult))
                        return percentResult;
                    return 0;
                }

                if (double.TryParse(value, out double result))
                    return result;
                return 0;
            }

            return new Player
            {
                // Core Combat Stats
                Kills = ParseStat(stats["Kills"]),
                Deaths = ParseStat(stats["Deaths"]),
                Assists = ParseStat(stats["Assists"]),
                ADR = ParseStat(stats["ADR"]),
                KDRatio = ParseStat(stats["K/D Ratio"]),
                KRRRatio = ParseStat(stats["K/R Ratio"]),
                Damage = ParseStat(stats["Damage"]),


                // Headshot Stats
                Headshots = ParseStat(stats["Headshots"]),
                HeadshotsPercentage = ParseStat(stats["Headshots %"]),

                // Multi-kill Stats
                DoubleKills = ParseStat(stats["Double Kills"]),
                TripleKills = ParseStat(stats["Triple Kills"]),
                QuadroKills = ParseStat(stats["Quadro Kills"]),
                PentaKills = ParseStat(stats["Penta Kills"]),

                // Flash Stats
                FlashCount = ParseStat(stats["Flash Count"]),
                EnemiesFlashed = ParseStat(stats["Enemies Flashed"]),
                FlashSuccesses = ParseStat(stats["Flash Successes"]),
                FlashesPerRound = ParseStat(stats["Flashes per Round in a Match"]),
                EnemiesFlashedPerRound = ParseStat(stats["Enemies Flashed per Round in a Match"]),
                FlashSuccessRatePerMatch = ParseStat(stats["Flash Success Rate per Match"]),

                // Utility Stats
                UtilityUsagePerRound = ParseStat(stats["Utility Usage per Round"]),
                UtilityCount = ParseStat(stats["Utility Count"]),
                UtilityDamage = ParseStat(stats["Utility Damage"]),
                UtilitySuccesses = ParseStat(stats["Utility Successes"]),
                UtilitySuccessRatePerMatch = ParseStat(stats["Utility Success Rate per Match"]),
                UtilityDamagePerRound = ParseStat(stats["Utility Damage per Round in a Match"]),
                UtilityDamageSuccessRatePerMatch = ParseStat(stats["Utility Damage Success Rate per Match"]),
                UtilityEnemies = ParseStat(stats["Utility Enemies"]),

                // Clutch Stats
                ClutchKills = ParseStat(stats["Clutch Kills"]),
                OneVOneCount = ParseStat(stats["1v1Count"]),
                OneVOneWins = ParseStat(stats["1v1Wins"]),
                MatchOneVOneWinRate = ParseStat(stats["Match 1v1 Win Rate"]),
                OneVTwoCount = ParseStat(stats["1v2Count"]),
                OneVTwoWins = ParseStat(stats["1v2Wins"]),
                MatchOneVTwoWinRate = ParseStat(stats["Match 1v2 Win Rate"]),

                // Weapon Stats
                PistolKills = ParseStat(stats["Pistol Kills"]),
                SniperKills = ParseStat(stats["Sniper Kills"]),
                SniperKillRatePerRound = ParseStat(stats["Sniper Kill Rate per Round"]),
                SniperKillRatePerMatch = ParseStat(stats["Sniper Kill Rate per Match"]),
                KnifeKills = ParseStat(stats["Knife Kills"]),
                ZeusKills = ParseStat(stats["Zeus Kills"]),

                // Entry Stats
                FirstKills = ParseStat(stats["First Kills"]),
                EntryCount = ParseStat(stats["Entry Count"]),
                EntryWins = ParseStat(stats["Entry Wins"]),
                MatchEntrySuccessRate = ParseStat(stats["Match Entry Success Rate"]),
                MatchEntryRate = ParseStat(stats["Match Entry Rate"]),

                // Other Stats
                MVPs = ParseStat(stats["MVPs"])
            };
        }


        private static double ParseStat(JToken stat)
        {
            if (stat == null)
                return 0;

            var value = stat.ToString();
            if (value.EndsWith("%"))
            {
                if (double.TryParse(value.TrimEnd('%'), out double percentResult))
                    return percentResult;
                return 0;
            }

            if (double.TryParse(value, out double result))
                return result;
            return 0;
        }

        private static void UpdateStats(Player target, Player source)
        {
            // Core Combat Stats
            target.Kills += source.Kills;
            target.Deaths += source.Deaths;
            target.Assists += source.Assists;
            target.ADR += source.ADR;
            target.KDRatio += source.KDRatio;
            target.KRRRatio += source.KRRRatio;
            target.Damage += source.Damage;

            // Headshot Stats
            target.Headshots += source.Headshots;
            target.HeadshotsPercentage += source.HeadshotsPercentage;

            // Multi-kill Stats
            target.DoubleKills += source.DoubleKills;
            target.TripleKills += source.TripleKills;
            target.QuadroKills += source.QuadroKills;
            target.PentaKills += source.PentaKills;

            // Flash Stats
            target.FlashCount += source.FlashCount;
            target.EnemiesFlashed += source.EnemiesFlashed;
            target.FlashSuccesses += source.FlashSuccesses;
            target.FlashesPerRound += source.FlashesPerRound;
            target.EnemiesFlashedPerRound += source.EnemiesFlashedPerRound;
            target.FlashSuccessRatePerMatch += source.FlashSuccessRatePerMatch;

            // Utility Stats
            target.UtilityUsagePerRound += source.UtilityUsagePerRound;
            target.UtilityCount += source.UtilityCount;
            target.UtilityDamage += source.UtilityDamage;
            target.UtilitySuccesses += source.UtilitySuccesses;
            target.UtilitySuccessRatePerMatch += source.UtilitySuccessRatePerMatch;
            target.UtilityDamagePerRound += source.UtilityDamagePerRound;
            target.UtilityDamageSuccessRatePerMatch += source.UtilityDamageSuccessRatePerMatch;
            target.UtilityEnemies += source.UtilityEnemies;

            // Clutch Stats
            target.ClutchKills += source.ClutchKills;
            target.OneVOneCount += source.OneVOneCount;
            target.OneVOneWins += source.OneVOneWins;
            target.MatchOneVOneWinRate += source.MatchOneVOneWinRate;
            target.OneVTwoCount += source.OneVTwoCount;
            target.OneVTwoWins += source.OneVTwoWins;
            target.MatchOneVTwoWinRate += source.MatchOneVTwoWinRate;

            // Weapon Stats
            target.PistolKills += source.PistolKills;
            target.SniperKills += source.SniperKills;
            target.SniperKillRatePerRound += source.SniperKillRatePerRound;
            target.SniperKillRatePerMatch += source.SniperKillRatePerMatch;
            target.KnifeKills += source.KnifeKills;
            target.ZeusKills += source.ZeusKills;

            // Entry Stats
            target.FirstKills += source.FirstKills;
            target.EntryCount += source.EntryCount;
            target.EntryWins += source.EntryWins;
            target.MatchEntrySuccessRate += (source.MatchEntrySuccessRate) * 100;
            target.MatchEntryRate += (source.MatchEntryRate) * 100;

            // Other Stats
            target.MVPs += source.MVPs;
        }

        private static void CalculateAverages(Player stats, int matchCount)
        {
            if (matchCount == 0)
                return;

            // Core Combat Stats
            stats.Kills /= matchCount;
            stats.Deaths /= matchCount;
            stats.Assists /= matchCount;
            stats.ADR /= matchCount;
            stats.KDRatio /= matchCount;
            stats.KRRRatio /= matchCount;
            stats.Damage /= matchCount;

            // Headshot Stats
            stats.Headshots /= matchCount;
            stats.HeadshotsPercentage /= matchCount;

            // Multi-kill Stats
            stats.DoubleKills /= matchCount;
            stats.TripleKills /= matchCount;
            stats.QuadroKills /= matchCount;
            stats.PentaKills /= matchCount;

            // Flash Stats
            stats.FlashCount /= matchCount;
            stats.EnemiesFlashed /= matchCount;
            stats.FlashSuccesses /= matchCount;
            stats.FlashesPerRound /= matchCount;
            stats.EnemiesFlashedPerRound /= matchCount;
            stats.FlashSuccessRatePerMatch /= matchCount;

            // Utility Stats
            stats.UtilityUsagePerRound /= matchCount;
            stats.UtilityCount /= matchCount;
            stats.UtilityDamage /= matchCount;
            stats.UtilitySuccesses /= matchCount;
            stats.UtilitySuccessRatePerMatch /= matchCount;
            stats.UtilityDamagePerRound /= matchCount;
            stats.UtilityDamageSuccessRatePerMatch /= matchCount;
            stats.UtilityEnemies /= matchCount;

            // Clutch Stats
            stats.ClutchKills /= matchCount;
            stats.OneVOneCount /= matchCount;
            stats.OneVOneWins /= matchCount;
            stats.MatchOneVOneWinRate /= matchCount;
            stats.OneVTwoCount /= matchCount;
            stats.OneVTwoWins /= matchCount;
            stats.MatchOneVTwoWinRate /= matchCount;

            // Weapon Stats
            stats.PistolKills /= matchCount;
            stats.SniperKills /= matchCount;
            stats.SniperKillRatePerRound /= matchCount;
            stats.SniperKillRatePerMatch /= matchCount;
            stats.KnifeKills /= matchCount;
            stats.ZeusKills /= matchCount;

            // Entry Stats
            stats.FirstKills /= matchCount;
            stats.EntryCount /= matchCount;
            stats.EntryWins /= matchCount;
            stats.MatchEntrySuccessRate /= matchCount;
            stats.MatchEntryRate /= matchCount;

            // Other Stats
            stats.MVPs /= matchCount;
        }

    }
}