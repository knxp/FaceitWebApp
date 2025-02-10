using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using faceitWebApp.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace faceitWebApp.Handlers
{
    public class ActivePlayersHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private const int MIN_MATCHES_THRESHOLD = 3;
        private const int MAX_PARALLEL_REQUESTS = 10;

        public ActivePlayersHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<List<ActivePlayer>> GetActivePlayersAsync(string teamId, string teamName, List<TeamPlayer> players, string seasonName)
        {
            // Validate season exists
            if (!SeasonDates.Seasons.TryGetValue(seasonName, out var seasonDates))
            {
                Console.WriteLine($"Invalid season: {seasonName}");
                return new List<ActivePlayer>();
            }

            // First get the team details to find the leader
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

            var teamResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");
            if (!teamResponse.IsSuccessStatusCode) return new List<ActivePlayer>();

            var teamContent = await teamResponse.Content.ReadAsStringAsync();
            var teamJson = JObject.Parse(teamContent);
            var leaderId = teamJson["leader"]?.ToString();

            if (string.IsNullOrEmpty(leaderId)) return new List<ActivePlayer>();

            var fromTimestamp = seasonDates.Start;
            var toTimestamp = seasonDates.End;

            // Get matches from the leader's history
            var response = await _httpClient.GetAsync(
                $"https://open.faceit.com/data/v4/players/{leaderId}/history?game=cs2&from={fromTimestamp}&to={toTimestamp}&offset=0&limit=100");

            if (!response.IsSuccessStatusCode) return new List<ActivePlayer>();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var matches = json["items"] as JArray;

            if (matches == null || !matches.Any()) return new List<ActivePlayer>();

            // Filter matches for this season
            var seasonMatches = matches.Where(match =>
            {
                var startedAt = long.Parse(match["started_at"].ToString());
                var competitionName = match["competition_name"]?.ToString() ?? "";
                return SeasonDates.IsMatchInSeason(startedAt, competitionName);
            }).ToList();

            var activePlayersList = new List<ActivePlayer>();
            var semaphore = new SemaphoreSlim(MAX_PARALLEL_REQUESTS);
            var playerTasks = new List<Task<(TeamPlayer Player, int MatchesPlayed)>>();

            // Process players in parallel
            foreach (var player in players)
            {
                playerTasks.Add(ProcessPlayerMatchesAsync(player, seasonMatches, semaphore));
            }

            var results = await Task.WhenAll(playerTasks);

            foreach (var result in results.Where(r => r.MatchesPlayed >= MIN_MATCHES_THRESHOLD))
            {
                var lastMatch = seasonMatches.First();
                var lastMatchDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(lastMatch["started_at"].ToString())).UtcDateTime;

                activePlayersList.Add(new ActivePlayer
                {
                    PlayerId = result.Player.PlayerId,
                    Nickname = result.Player.Nickname,
                    Avatar = result.Player.Avatar,
                    Elo = result.Player.Elo,
                    LastMatchDate = lastMatchDate,
                    MatchesWithTeam = result.MatchesPlayed,
                    IsActive = true
                });
            }

            return activePlayersList.OrderByDescending(p => p.MatchesWithTeam).ToList();
        }

        private async Task<(TeamPlayer Player, int MatchesPlayed)> ProcessPlayerMatchesAsync(TeamPlayer player, List<JToken> seasonMatches, SemaphoreSlim semaphore)
        {
            var matchesPlayed = 0;

            foreach (var match in seasonMatches)
            {
                await semaphore.WaitAsync();
                try
                {
                    var matchId = match["match_id"].ToString();
                    var matchResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/matches/{matchId}/stats");

                    if (!matchResponse.IsSuccessStatusCode) continue;

                    var matchContent = await matchResponse.Content.ReadAsStringAsync();
                    var matchJson = JObject.Parse(matchContent);
                    var rounds = matchJson["rounds"] as JArray;

                    if (rounds == null || !rounds.Any()) continue;

                    foreach (var round in rounds)
                    {
                        var teams = round["teams"] as JArray;
                        if (teams == null) continue;

                        foreach (var team in teams)
                        {
                            var matchPlayers = team["players"] as JArray;
                            if (matchPlayers != null && matchPlayers.Any(p => p["player_id"]?.ToString() == player.PlayerId))
                            {
                                matchesPlayed++;
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            return (player, matchesPlayed);
        }
    }
}
