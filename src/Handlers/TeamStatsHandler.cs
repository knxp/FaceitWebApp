using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using faceitWebApp.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace faceitWebApp.Handlers
{
    public class TeamStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;

        public TeamStatsHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["faceitapikey"];
        }

        public async Task<(TeamInfo info, TeamStats stats, List<MapStats> mapStats)> GetTeamStatsAsync(string teamId, string gameId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // Create tasks for parallel execution
                var infoTask = _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");
                var statsTask = _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}/stats/{gameId}");

                // Wait for both requests to complete
                await Task.WhenAll(infoTask, statsTask);

                // Process team info
                var infoResponse = await infoTask;
                infoResponse.EnsureSuccessStatusCode();
                var infoJson = await infoResponse.Content.ReadAsStringAsync();
                var infoData = JObject.Parse(infoJson);

                var teamInfo = new TeamInfo
                {
                    Name = infoData["name"]?.ToString(),
                    Avatar = infoData["avatar"]?.ToString(),
                    GameId = gameId,
                    Players = new List<TeamPlayer>()
                };

                // Process team stats in parallel
                var statsResponse = await statsTask;
                statsResponse.EnsureSuccessStatusCode();
                var statsJson = await statsResponse.Content.ReadAsStringAsync();
                var statsData = JObject.Parse(statsJson);

                // Create player data tasks
                var members = infoData["members"] as JArray;
                var playerTasks = new List<Task>();
                var playerDataLock = new object();

                if (members != null)
                {
                    foreach (var member in members)
                    {
                        var playerId = member["user_id"]?.ToString();
                        var nickname = member["nickname"]?.ToString();
                        var avatar = member["avatar"]?.ToString();

                        var playerTask = Task.Run(async () =>
                        {
                            try
                            {
                                var playerResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players/{playerId}");
                                if (playerResponse.IsSuccessStatusCode)
                                {
                                    var playerJson = await playerResponse.Content.ReadAsStringAsync();
                                    var playerData = JObject.Parse(playerJson);
                                    var games = playerData["games"] as JObject;
                                    var cs2Data = games?["cs2"] as JObject;
                                    var elo = cs2Data?["faceit_elo"]?.Value<int>() ?? 0;

                                    var player = new TeamPlayer
                                    {
                                        Nickname = nickname,
                                        PlayerId = playerId,
                                        Avatar = avatar,
                                        Elo = elo
                                    };

                                    lock (playerDataLock)
                                    {
                                        teamInfo.Players.Add(player);
                                    }
                                }
                            }
                            catch
                            {
                                // If player data fetch fails, add player with default elo
                                lock (playerDataLock)
                                {
                                    teamInfo.Players.Add(new TeamPlayer
                                    {
                                        Nickname = nickname,
                                        PlayerId = playerId,
                                        Avatar = avatar,
                                        Elo = 0
                                    });
                                }
                            }
                        });

                        playerTasks.Add(playerTask);
                    }

                    // Wait for all player data to be processed with a timeout
                    await Task.WhenAll(playerTasks).WaitAsync(TimeSpan.FromSeconds(10));
                }

                // Process team stats
                var teamStats = new TeamStats
                {
                    TotalMatches = int.Parse(statsData["lifetime"]?["Matches"]?.ToString() ?? "0"),
                    WinCount = int.Parse(statsData["lifetime"]?["Wins"]?.ToString() ?? "0"),
                    CurrentStreak = int.Parse(statsData["lifetime"]?["Current Win Streak"]?.ToString() ?? "0"),
                    LongestWinStreak = statsData["lifetime"]?["Longest Win Streak"]?.ToString() ?? "0",
                    RecentMatches = new List<TeamMatchHistory>()
                };

                // Process recent results
                var recentResults = statsData["lifetime"]?["Recent Results"] as JArray;
                if (recentResults != null)
                {
                    teamStats.RecentMatches.AddRange(
                        recentResults.Take(5).Select(result => new TeamMatchHistory
                        {
                            MatchId = Guid.NewGuid().ToString(),
                            TeamId = teamId,
                            Result = result.ToString() == "1" ? 1 : 0
                        })
                    );
                }

                // Process map stats
                var mapStats = new List<MapStats>();
                var segments = statsData["segments"] as JArray;
                if (segments != null)
                {
                    mapStats.AddRange(
                        segments
                            .Where(segment =>
                            {
                                var mapName = segment["label"]?.ToString();
                                return !string.IsNullOrEmpty(mapName) &&
                                       !mapName.Contains("wingman", StringComparison.OrdinalIgnoreCase);
                            })
                            .Select(segment => new MapStats
                            {
                                Map = segment["label"].ToString(),
                                TotalMatches = int.Parse(segment["stats"]?["Matches"]?.ToString() ?? "0"),
                                Wins = int.Parse(segment["stats"]?["Wins"]?.ToString() ?? "0")
                            })
                    );
                }

                return (teamInfo, teamStats, mapStats);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching team stats: Try using the Faceit Team Url");
            }
        }
    }
}