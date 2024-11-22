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
    public class ActivePlayersHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private const int MIN_MATCHES_THRESHOLD = 3;
        private static readonly DateTime START_DATE = new DateTime(2024, 10, 6);
        private static readonly DateTime END_DATE = new DateTime(2024, 12, 15);
        private const int MAX_PARALLEL_REQUESTS = 5;
        private const int BATCH_SIZE = 10;

        public ActivePlayersHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<List<ActivePlayer>> GetActivePlayersAsync(string teamId, string teamName, List<TeamPlayer> players)
        {
            var activePlayersList = new List<ActivePlayer>();
            var semaphore = new SemaphoreSlim(MAX_PARALLEL_REQUESTS);
            var tasks = new List<Task<ActivePlayer>>();

            // Process players in parallel batches
            for (int i = 0; i < players.Count; i += BATCH_SIZE)
            {
                var batch = players.Skip(i).Take(BATCH_SIZE);
                var batchTasks = batch.Select(player => ProcessPlayerActivityAsync(player, teamId, teamName, semaphore));
                tasks.AddRange(batchTasks);
            }

            var results = await Task.WhenAll(tasks);
            activePlayersList.AddRange(results.Where(p => p != null));

            return activePlayersList.OrderByDescending(p => p.MatchesWithTeam).ToList();
        }

        private async Task<ActivePlayer> ProcessPlayerActivityAsync(TeamPlayer player, string teamId, string teamName, SemaphoreSlim semaphore)
        {
            try
            {
                await semaphore.WaitAsync();
                try
                {
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                    var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players/{player.PlayerId}/history?game=cs2&offset=0&limit=100");
                    if (!response.IsSuccessStatusCode) return null;

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var items = json["items"] as JArray;

                    if (items == null || !items.Any()) return null;

                    var teamMatches = items
                        .Where(match =>
                        {
                            var teams = match["teams"] as JObject;
                            return teams != null &&
                                   (teams["faction1"]?["team_id"]?.ToString() == teamId ||
                                    teams["faction2"]?["team_id"]?.ToString() == teamId) &&
                                    match["competition_name"]?.ToString().Contains("ESEA S51") == true;
                        })
                        .ToList();

                    if (!teamMatches.Any()) return null;

                    var matchTasks = teamMatches.Select(match => GetMatchDetailsAsync(match, player.PlayerId));
                    var matchResults = await Task.WhenAll(matchTasks);
                    var matchesPlayed = matchResults.Count(played => played);

                    var lastMatch = teamMatches.First();
                    var lastMatchDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(lastMatch["started_at"].ToString())).UtcDateTime;

                    return new ActivePlayer
                    {
                        PlayerId = player.PlayerId,
                        Nickname = player.Nickname,
                        Avatar = player.Avatar,
                        Elo = player.Elo,
                        LastMatchDate = lastMatchDate,
                        MatchesWithTeam = matchesPlayed,
                        IsActive = matchesPlayed >= MIN_MATCHES_THRESHOLD
                    };
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing player {player.Nickname}: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> GetMatchDetailsAsync(JToken match, string playerId)
        {
            try
            {
                var matchId = match["match_id"]?.ToString();
                if (string.IsNullOrEmpty(matchId)) return false;

                var matchResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/matches/{matchId}/stats");
                if (!matchResponse.IsSuccessStatusCode) return false;

                var matchContent = await matchResponse.Content.ReadAsStringAsync();
                var matchJson = JObject.Parse(matchContent);
                var rounds = matchJson["rounds"] as JArray;

                if (rounds == null || !rounds.Any()) return false;

                var firstRound = rounds[0];
                var teams = firstRound["teams"] as JArray;

                if (teams == null) return false;

                foreach (var team in teams)
                {
                    var players = team["players"] as JArray;
                    if (players != null && players.Any(p => p["player_id"]?.ToString() == playerId))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
