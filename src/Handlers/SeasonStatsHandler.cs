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
        private readonly IConfiguration _configuration;
        private readonly FaceitService _faceitService;
        private readonly ActivePlayersHandler _activePlayersHandler;
        private const int BATCH_SIZE = 10;
        private const int MAX_PARALLEL_REQUESTS = 10;

        public SeasonStatsHandler(HttpClient httpClient, IConfiguration configuration, FaceitService faceitService, ActivePlayersHandler activePlayersHandler)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _faceitService = faceitService;
            _activePlayersHandler = activePlayersHandler;
        }

        private async Task<string> GetFaceitApiKey()
        {
            return await _faceitService.GetFaceitApiKey();
        }

        public async Task<List<MapStats>> GetSeasonMapStatsAsync(string teamId, string teamName, List<TeamPlayer> players, string seasonName)
        {
            var mapStats = new Dictionary<string, MapStats>();

            // Get season dates from the dictionary
            if (!SeasonDates.Seasons.TryGetValue(seasonName, out var seasonDates))
            {
                return new List<MapStats>();
            }

            try
            {
                var faceitApiKey = await GetFaceitApiKey();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + faceitApiKey);

                // First, get the team leader's ID
                var teamResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");
                if (!teamResponse.IsSuccessStatusCode)
                {
                    return new List<MapStats>();
                }

                var teamContent = await teamResponse.Content.ReadAsStringAsync();
                var teamJson = JObject.Parse(teamContent);
                var leaderId = teamJson["leader"]?.ToString();

                if (string.IsNullOrEmpty(leaderId))
                {
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
                    return new List<MapStats>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var matches = json["items"] as JArray;

                if (matches == null)
                {
                    return new List<MapStats>();
                }

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
                            // Continue with next match
                        }
                    });

                    await Task.WhenAll(batchTasks);
                }

                return mapStats.Values.OrderByDescending(m => m.WinRate).ToList();
            }
            catch (Exception ex)
            {
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
                    var matchResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/matches/{matchId}/stats");
                    if (!matchResponse.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var matchContent = await matchResponse.Content.ReadAsStringAsync();
                    var matchJson = JObject.Parse(matchContent);
                    var rounds = matchJson["rounds"] as JArray;

                    if (rounds == null || !rounds.Any())
                    {
                        return;
                    }

                    // Process each round/map in the match
                    foreach (var round in rounds)
                    {
                        var mapName = round["round_stats"]?["Map"]?.ToString();
                        if (string.IsNullOrEmpty(mapName))
                        {
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
                // Continue with next match
            }
        }
    }
}