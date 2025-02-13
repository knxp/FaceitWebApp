using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using faceitWebApp.Dictionaries;
using faceitWebApp.Models;
using System.Linq;
using faceitWebApp.Services;

namespace faceitWebApp.Handlers
{
    public class FullStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly FaceitService _faceitService;
        private readonly HashSet<string> _percentageStats = new HashSet<string>
        {
            "Match Entry Rate",
            "Match Entry Success Rate",
            "Match 1v1 Win Rate",
            "Match 1v2 Win Rate",
            "Sniper Kill Rate per Match",
            "Flash Success Rate per Match",
            "Utility Success Rate per Match",
            "Utility Damage Success Rate per Match"
        };

        private readonly RatingHandler _ratingHandler;

        public FullStatsHandler(HttpClient httpClient, IConfiguration configuration, FaceitService faceitService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _faceitService = faceitService;
            _ratingHandler = new RatingHandler();
        }

        private async Task<string> GetFaceitApiKey()
        {
            return await _faceitService.GetFaceitApiKey();
        }

        public async Task<Player> GetFullStatsAsync(string playerId, int matchLimit = 100)
        {
            var player = new Player { Id = playerId };
            var stats = new Dictionary<string, double>();
            var matchTasks = new List<Task<Dictionary<string, double>>>();

            foreach (var key in FullStatsDictionary.Stats.Keys)
            {
                stats[key] = 0;
            }

            try
            {
                var faceitApiKey = await GetFaceitApiKey();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + faceitApiKey);

                var matchHistoryUrl = $"https://open.faceit.com/data/v4/players/{playerId}/history?game=cs2&offset=0&limit=100";
                var response = await _httpClient.GetAsync(matchHistoryUrl);
                response.EnsureSuccessStatusCode();

                var matchHistory = JObject.Parse(await response.Content.ReadAsStringAsync());
                var matches = matchHistory["items"] as JArray;

                if (matches != null)
                {
                    var matchmakingMatches = matches
                        .Where(m => m["competition_type"]?.ToString() == "matchmaking")
                        .Take(matchLimit)
                        .ToList();

                    foreach (var match in matchmakingMatches)
                    {
                        var matchId = match["match_id"].ToString();
                        matchTasks.Add(ProcessMatchAsync(matchId, playerId));
                    }

                    var results = await Task.WhenAll(matchTasks);
                    var validResults = results.Where(r => r != null).ToList();

                    foreach (var matchStats in validResults)
                    {
                        foreach (var stat in matchStats)
                        {
                            stats[stat.Key] += stat.Value;
                        }
                    }

                    int matchCount = validResults.Count;
                    if (matchCount > 0)
                    {
                        // Core Combat Stats
                        player.Kills = Math.Round(stats["Kills"] / matchCount, 2);
                        player.Assists = Math.Round(stats["Assists"] / matchCount, 2);
                        player.Deaths = Math.Round(stats["Deaths"] / matchCount, 2);
                        player.ADR = Math.Round(stats["ADR"] / matchCount, 2);
                        player.KDRatio = Math.Round(stats["K/D Ratio"] / matchCount, 2);
                        player.KRRRatio = Math.Round(stats["K/R Ratio"] / matchCount, 2);
                        player.Damage = Math.Round(stats["Damage"] / matchCount, 2);

                        // Headshot Stats
                        player.Headshots = Math.Round(stats["Headshots"] / matchCount, 2);
                        player.HeadshotsPercentage = Math.Round(stats["Headshots %"] / matchCount, 2);

                        // Multi-kill Stats
                        player.DoubleKills = Math.Round(stats["Double Kills"] / matchCount, 2);
                        player.TripleKills = Math.Round(stats["Triple Kills"] / matchCount, 2);
                        player.QuadroKills = Math.Round(stats["Quadro Kills"] / matchCount, 2);
                        player.PentaKills = Math.Round(stats["Penta Kills"] / matchCount, 2);

                        // Flash Stats
                        player.FlashCount = Math.Round(stats["Flash Count"] / matchCount, 2);
                        player.EnemiesFlashed = Math.Round(stats["Enemies Flashed"] / matchCount, 2);
                        player.FlashSuccesses = Math.Round(stats["Flash Successes"] / matchCount, 2);
                        player.FlashesPerRound = Math.Round(stats["Flashes per Round in a Match"] / matchCount, 2);
                        player.EnemiesFlashedPerRound = Math.Round(stats["Enemies Flashed per Round in a Match"] / matchCount, 2);
                        player.FlashSuccessRatePerMatch = Math.Round(stats["Flash Success Rate per Match"] / matchCount, 2);

                        // Utility Stats
                        player.UtilityCount = Math.Round(stats["Utility Count"] / matchCount, 2);
                        player.UtilityDamage = Math.Round(stats["Utility Damage"] / matchCount, 2);
                        player.UtilitySuccesses = Math.Round(stats["Utility Successes"] / matchCount, 2);
                        player.UtilityUsagePerRound = Math.Round(stats["Utility Usage per Round"] / matchCount, 2);
                        player.UtilitySuccessRatePerMatch = Math.Round(stats["Utility Success Rate per Match"] / matchCount, 2);
                        player.UtilityDamagePerRound = Math.Round(stats["Utility Damage per Round in a Match"] / matchCount, 2);
                        player.UtilityDamageSuccessRatePerMatch = Math.Round(stats["Utility Damage Success Rate per Match"] / matchCount, 2);
                        player.UtilityEnemies = Math.Round(stats["Utility Enemies"] / matchCount, 2);

                        // Clutch Stats
                        player.ClutchKills = Math.Round(stats["Clutch Kills"] / matchCount, 2);
                        player.OneVOneCount = Math.Round(stats["1v1Count"] / matchCount, 2);
                        player.OneVOneWins = Math.Round(stats["1v1Wins"] / matchCount, 2);
                        player.MatchOneVOneWinRate = Math.Round(stats["1v1Count"] / stats["1v1Wins"], 2);
                        player.OneVTwoCount = Math.Round(stats["1v2Count"] / matchCount, 2);
                        player.OneVTwoWins = Math.Round(stats["1v2Wins"] / matchCount, 2);
                        player.MatchOneVTwoWinRate = Math.Round(stats["Match 1v2 Win Rate"] / matchCount, 2);

                        // Weapon Stats
                        player.PistolKills = Math.Round(stats["Pistol Kills"] / matchCount, 2);
                        player.SniperKills = Math.Round(stats["Sniper Kills"] / matchCount, 2);
                        player.SniperKillRatePerRound = Math.Round(stats["Sniper Kill Rate per Round"] / matchCount, 2);
                        player.SniperKillRatePerMatch = Math.Round(stats["Sniper Kill Rate per Match"] / matchCount, 2);
                        player.KnifeKills = Math.Round(stats["Knife Kills"], 2);
                        player.ZeusKills = Math.Round(stats["Zeus Kills"], 2);

                        // Entry Stats
                        player.FirstKills = Math.Round(stats["First Kills"] / matchCount, 2);
                        player.EntryCount = Math.Round(stats["Entry Count"] / matchCount, 2);
                        player.EntryWins = Math.Round(stats["Entry Wins"] / matchCount, 2);
                        player.MatchEntrySuccessRate = Math.Round(stats["Match Entry Success Rate"] / matchCount, 2);
                        player.MatchEntryRate = Math.Round(stats["Match Entry Rate"] / matchCount, 2);

                        // Other Stats
                        player.MVPs = Math.Round(stats["MVPs"] / matchCount, 2);
                        player.Result = Math.Round(stats["Result"] / matchCount, 2);

                        // Calculate rating here, inside the if block
                        player.Rating = _ratingHandler.CalculateRating(player, stats["TotalRounds"], validResults.Count);
                        Console.WriteLine($"Player Rating: {player.Rating} (over {stats["TotalRounds"]} rounds)");
                    }
                }

                return player;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching detailed player stats: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, double>> ProcessMatchAsync(string matchId, string playerId)
        {
            try
            {
                var matchStatsUrl = $"https://open.faceit.com/data/v4/matches/{matchId}/stats";
                var matchResponse = await _httpClient.GetAsync(matchStatsUrl);

                if (!matchResponse.IsSuccessStatusCode)
                    return null;

                var matchStats = JObject.Parse(await matchResponse.Content.ReadAsStringAsync());
                var rounds = matchStats["rounds"] as JArray;

                if (rounds == null || !rounds.Any())
                    return null;

                var matchStatValues = new Dictionary<string, double>();

                // Get total rounds from round_stats
                var roundStats = rounds[0]["round_stats"];
                if (roundStats != null && roundStats["Rounds"] != null)
                {
                    matchStatValues["TotalRounds"] = double.Parse(roundStats["Rounds"].ToString());
                }

                var playerStats = rounds[0]["teams"]
                    .SelectMany(t => t["players"])
                    .FirstOrDefault(p => p["player_id"]?.ToString() == playerId)?["player_stats"] as JObject;

                // Rest of the stats processing
                foreach (var key in FullStatsDictionary.Stats.Keys)
                {
                    if (key == "TotalRounds")
                        continue; // Skip as we've already handled it

                    if (playerStats[key] != null)
                    {
                        var value = playerStats[key].ToString();
                        if (value.EndsWith("%"))
                        {
                            value = value.TrimEnd('%');
                        }

                        if (double.TryParse(value, out double numValue))
                        {
                            // For percentage stats that come as decimals (0-1), multiply by 100
                            if (_percentageStats.Contains(key) && numValue <= 1)
                            {
                                numValue *= 100;
                            }
                            matchStatValues[key] = numValue;
                        }
                        else
                        {
                            matchStatValues[key] = 0;
                        }
                    }
                    else
                    {
                        matchStatValues[key] = 0;
                    }
                }

                return matchStatValues;
            }
            catch
            {
                return null;
            }
        }
    }
}