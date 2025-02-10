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
        private const int BATCH_SIZE = 10;
        private const int MAX_PARALLEL_REQUESTS = 10;

        public SeasonStatsHandler(HttpClient httpClient, IConfiguration configuration, ActivePlayersHandler activePlayersHandler)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
            _activePlayersHandler = activePlayersHandler;
        }

        public async Task<List<MapStats>> GetSeasonMapStatsAsync(string teamId, string teamName, List<TeamPlayer> players, string seasonName)
        {
            var mapStats = new Dictionary<string, MapStats>();

            // Get season dates from the dictionary
            if (!SeasonDates.Seasons.TryGetValue(seasonName, out var seasonDates))
            {
                Console.WriteLine($"Invalid season: {seasonName}");
                return new List<MapStats>();
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // First, get the team leader's ID
                var teamResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");
                if (!teamResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get team info: {teamResponse.StatusCode}");
                    return new List<MapStats>();
                }

                var teamContent = await teamResponse.Content.ReadAsStringAsync();
                var teamJson = JObject.Parse(teamContent);
                var leaderId = teamJson["leader"]?.ToString();

                if (string.IsNullOrEmpty(leaderId))
                {
                    Console.WriteLine("No team leader found");
                    return new List<MapStats>();
                }

                // Use Unix timestamps directly from SeasonDates
                var fromTimestamp = seasonDates.Start;
                var toTimestamp = seasonDates.End;

                // Get matches using the team leader's ID
                var response = await _httpClient.GetAsync(
                    $"https://open.faceit.com/data/v4/players/{leaderId}/history?game=cs2&limit=100&from={fromTimestamp}&to={toTimestamp}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get match history: {response.StatusCode}");
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
                        var teams = match["teams"] as JObject;
                        var startedAt = long.Parse(match["started_at"].ToString());
                        var competitionName = match["competition_name"]?.ToString() ?? "";

                        return teams != null &&
                               (teams["faction1"]?["team_id"]?.ToString() == teamId ||
                                teams["faction2"]?["team_id"]?.ToString() == teamId) &&
                               SeasonDates.IsMatchInSeason(startedAt, competitionName);
                    })
                    .ToList();

                Console.WriteLine($"Team matches found: {seasonMatches.Count}");

                // Process matches in parallel batches
                var semaphore = new SemaphoreSlim(MAX_PARALLEL_REQUESTS);
                var batches = (int)Math.Ceiling(seasonMatches.Count / (double)BATCH_SIZE);

                for (int i = 0; i < batches; i++)
                {
                    var batchMatches = seasonMatches
                        .Skip(i * BATCH_SIZE)
                        .Take(BATCH_SIZE);

                    var batchTasks = batchMatches.Select(async match =>
                    {
                        try
                        {
                            await ProcessMatchAsync(match, teamId, mapStats, semaphore);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Skipping match {match["match_id"]}: {ex.Message}");
                            // Continue with next match
                        }
                    });

                    await Task.WhenAll(batchTasks);
                }

                return mapStats.Values.OrderByDescending(m => m.WinRate).ToList();
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
                        Console.WriteLine($"Skipping match {matchId}: Response --- {matchResponse.StatusCode}");
                        return;
                    }

                    var matchContent = await matchResponse.Content.ReadAsStringAsync();
                    var matchJson = JObject.Parse(matchContent);
                    var rounds = matchJson["rounds"] as JArray;

                    if (rounds == null || !rounds.Any())
                    {
                        Console.WriteLine($"Skipping match {matchId}: No rounds data found");
                        return;
                    }

                    // Process each round/map in the match
                    foreach (var round in rounds)
                    {
                        var mapName = round["round_stats"]?["Map"]?.ToString();
                        if (string.IsNullOrEmpty(mapName))
                        {
                            Console.WriteLine($"No map name found for match {matchId}");
                            continue;
                        }

                        lock (mapStats)
                        {
                            if (!mapStats.ContainsKey(mapName))
                            {
                                mapStats[mapName] = new MapStats { Map = mapName };
                            }

                            mapStats[mapName].TotalMatches++;

                            // Check the winner for this specific round
                            var roundWinner = round["round_stats"]?["Winner"]?.ToString();
                            if (roundWinner == teamId)
                            {
                                mapStats[mapName].Wins++;
                            }

                            Console.WriteLine($"Updated stats for {mapName}: Matches={mapStats[mapName].TotalMatches}, Wins={mapStats[mapName].Wins}");
                        }
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