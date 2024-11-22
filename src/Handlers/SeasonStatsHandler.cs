using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using faceitWebApp.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace faceitWebApp.Handlers
{
    public class SeasonStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private readonly ActivePlayersHandler _activePlayersHandler;
        private static readonly DateTime SEASON_START = new DateTime(2024, 10, 5);
        private static readonly DateTime SEASON_END = new DateTime(2024, 12, 14);
        private const int BATCH_SIZE = 10;
        private const int MAX_PARALLEL_REQUESTS = 5;

        public SeasonStatsHandler(HttpClient httpClient, IConfiguration configuration, ActivePlayersHandler activePlayersHandler)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
            _activePlayersHandler = activePlayersHandler;
        }

        public async Task<List<MapStats>> GetSeasonMapStatsAsync(string teamId, string teamName, List<TeamPlayer> players)
        {
            var mapStats = new Dictionary<string, MapStats>();
            var activePlayers = await _activePlayersHandler.GetActivePlayersAsync(teamId, teamName, players);

            // Get the player with the most matches
            var mostActivePlayer = activePlayers
                .OrderByDescending(p => p.MatchesWithTeam)
                .FirstOrDefault();

            if (mostActivePlayer == null)
            {
                Console.WriteLine("No active players found");
                return new List<MapStats>();
            }

            Console.WriteLine($"Most active player: {mostActivePlayer.Nickname} with {mostActivePlayer.MatchesWithTeam} matches");

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players/{mostActivePlayer.PlayerId}/history?game=cs2&offset=0&limit=100");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get player history: {response.StatusCode}");
                    return new List<MapStats>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var matches = json["items"] as JArray;

                if (matches == null)
                {
                    Console.WriteLine("No matches found in response");
                    return new List<MapStats>();
                }

                Console.WriteLine($"Total matches found: {matches.Count}");

                var seasonMatches = matches
                    .Where(match =>
                    {
                        var startedAt = long.Parse(match["started_at"].ToString());
                        var matchDate = DateTimeOffset.FromUnixTimeSeconds(startedAt).DateTime.ToUniversalTime();
                        var competitionName = match["competition_name"]?.ToString() ?? "";
                        var isInSeason = matchDate >= SEASON_START && matchDate <= SEASON_END;
                        var isEseaS51 = competitionName.Contains("ESEA S51", StringComparison.OrdinalIgnoreCase);

                        if (isInSeason && isEseaS51)
                        {
                            Console.WriteLine($"Found season match: {matchDate} - {competitionName}");
                            return true;
                        }
                        return false;
                    })
                    .ToList();

                Console.WriteLine($"Season matches found: {seasonMatches.Count}");

                // Process matches in parallel batches
                var semaphore = new SemaphoreSlim(MAX_PARALLEL_REQUESTS);
                var tasks = new List<Task>();
                var batches = (int)Math.Ceiling(seasonMatches.Count / (double)BATCH_SIZE);

                for (int i = 0; i < batches; i++)
                {
                    var batchMatches = seasonMatches
                        .Skip(i * BATCH_SIZE)
                        .Take(BATCH_SIZE);

                    foreach (var match in batchMatches)
                    {
                        tasks.Add(ProcessMatchAsync(match, teamId, mapStats, semaphore));
                    }
                }

                await Task.WhenAll(tasks);

                var result = mapStats.Values.OrderByDescending(m => m.WinRate).ToList();
                Console.WriteLine($"Final map stats count: {result.Count}");
                foreach (var stat in result)
                {
                    Console.WriteLine($"Map: {stat.Map}, Matches: {stat.TotalMatches}, Wins: {stat.Wins}, WinRate: {stat.WinRate:F1}%");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting season stats: {ex.Message}");
                return new List<MapStats>();
            }
        }

        private async Task ProcessMatchAsync(JToken match, string teamId, Dictionary<string, MapStats> mapStats, SemaphoreSlim semaphore)
        {
            try
            {
                var teams = match["teams"] as JObject;
                if (teams == null) return;

                string teamFaction = null;
                if (teams["faction1"]?["team_id"]?.ToString() == teamId)
                {
                    teamFaction = "faction1";
                }
                else if (teams["faction2"]?["team_id"]?.ToString() == teamId)
                {
                    teamFaction = "faction2";
                }
                else return;

                await semaphore.WaitAsync();
                try
                {
                    var matchId = match["match_id"].ToString();
                    Console.WriteLine($"Processing match: {matchId}");

                    var matchResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/matches/{matchId}/stats");
                    if (!matchResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Failed to get match stats: {matchResponse.StatusCode}");
                        return;
                    }

                    var matchContent = await matchResponse.Content.ReadAsStringAsync();
                    var matchJson = JObject.Parse(matchContent);
                    var rounds = matchJson["rounds"] as JArray;

                    if (rounds == null || !rounds.Any())
                    {
                        Console.WriteLine($"No rounds found for match {matchId}");
                        return;
                    }

                    var mapName = rounds[0]["round_stats"]?["Map"]?.ToString();
                    if (string.IsNullOrEmpty(mapName))
                    {
                        Console.WriteLine($"No map name found for match {matchId}");
                        return;
                    }

                    lock (mapStats)
                    {
                        if (!mapStats.ContainsKey(mapName))
                        {
                            mapStats[mapName] = new MapStats { Map = mapName };
                        }

                        mapStats[mapName].TotalMatches++;
                        if (match["results"]?["winner"]?.ToString() == teamFaction)
                        {
                            mapStats[mapName].Wins++;
                        }

                        Console.WriteLine($"Updated stats for {mapName}: Matches={mapStats[mapName].TotalMatches}, Wins={mapStats[mapName].Wins}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing match: {ex.Message}");
            }
        }
    }
}