using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using faceitWebApp.Dictionaries;
using faceitWebApp.Models;
using System.Linq;

namespace faceitWebApp.Handlers
{
    public class BasicStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private const int MaxMatchLimit = 300;

        public BasicStatsHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<Player> GetAverageBasicStatsAsync(string playerId, int matchLimit = 100)
        {
            // Ensure matchLimit is within bounds
            matchLimit = Math.Min(Math.Max(1, matchLimit), MaxMatchLimit);

            var player = new Player { Id = playerId };
            var stats = new Dictionary<string, double>();
            var matchTasks = new List<Task<Dictionary<string, double>>>();

            foreach (var key in BasicStatDictionary.Stats.Keys)
            {
                stats[key] = 0;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // Calculate required API calls based on match limit
                int requiredCalls = (int)Math.Ceiling(matchLimit / 100.0);
                var allMatches = new List<JToken>();

                // Fetch matches in batches of 100
                for (int i = 0; i < requiredCalls; i++)
                {
                    var offset = i * 100;
                    var matchHistoryUrl = $"https://open.faceit.com/data/v4/players/{playerId}/history?game=cs2&offset={offset}&limit=100";
                    var response = await _httpClient.GetAsync(matchHistoryUrl);
                    response.EnsureSuccessStatusCode();

                    var matchHistory = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var matches = matchHistory["items"] as JArray;

                    if (matches == null || !matches.Any())
                        break;

                    allMatches.AddRange(matches);
                }

                if (allMatches.Any())
                {
                    // Filter matchmaking matches and take requested number
                    var matchmakingMatches = allMatches
                        .Where(m => m["competition_type"]?.ToString() == "matchmaking")
                        .Take(matchLimit)
                        .ToList();

                    // Create tasks for parallel processing
                    foreach (var match in matchmakingMatches)
                    {
                        var matchId = match["match_id"].ToString();
                        matchTasks.Add(ProcessMatchAsync(matchId, playerId));
                    }

                    // Wait for all match processing tasks to complete
                    var results = await Task.WhenAll(matchTasks);

                    // Aggregate results
                    foreach (var matchStats in results.Where(r => r != null))
                    {
                        foreach (var stat in matchStats)
                        {
                            stats[stat.Key] += stat.Value;
                        }
                    }

                    int matchCount = results.Count(r => r != null);
                    if (matchCount > 0)
                    {
                        player.Kills = Math.Round(stats["Kills"] / matchCount, 2);
                        player.Assists = Math.Round(stats["Assists"] / matchCount, 2);
                        player.Deaths = Math.Round(stats["Deaths"] / matchCount, 2);
                        player.KDRatio = Math.Round(stats["K/D Ratio"] / matchCount, 2);
                        player.KRRRatio = Math.Round(stats["K/R Ratio"] / matchCount, 2);
                        player.Headshots = Math.Round(stats["Headshots"] / matchCount, 2);
                        player.HeadshotsPercentage = Math.Round(stats["Headshots %"] / matchCount, 2);
                        player.TripleKills = Math.Round(stats["Triple Kills"] / matchCount, 2);
                        player.QuadroKills = Math.Round(stats["Quadro Kills"] / matchCount, 2);
                        player.PentaKills = Math.Round(stats["Penta Kills"] / matchCount, 2);
                        player.MVPs = Math.Round(stats["MVPs"] / matchCount, 2);
                    }
                }

                return player;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching player stats: {ex.Message}");
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

                var playerStats = rounds[0]["teams"]
                    .SelectMany(t => t["players"])
                    .FirstOrDefault(p => p["player_id"]?.ToString() == playerId)?["player_stats"] as JObject;

                if (playerStats == null)
                    return null;

                var matchStatValues = new Dictionary<string, double>();
                foreach (var key in BasicStatDictionary.Stats.Keys)
                {
                    if (playerStats[key] != null)
                    {
                        matchStatValues[key] = playerStats[key].Value<double>();
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