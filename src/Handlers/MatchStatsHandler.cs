using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using faceitWebApp.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;

namespace faceitWebApp.Handlers
{
    public class MatchStatsHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;

        public MatchStatsHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<Match> GetMatchStatsAsync(string matchId)
        {
            try
            {
                // Ensure the match ID is in the correct format (1-{uuid})
                if (!matchId.StartsWith("1-"))
                {
                    matchId = $"1-{matchId}";
                }

                // Set authorization header
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // Use the complete API endpoint URL
                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/matches/{matchId}/stats");

                if (!response.IsSuccessStatusCode)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            throw new Exception("Authentication failed. Please check your API key.");
                        case HttpStatusCode.Forbidden:
                            throw new Exception("Access denied. Please verify your API key has the correct permissions.");
                        case HttpStatusCode.NotFound:
                            throw new Exception("Match not found. Please verify the match ID.");
                        default:
                            throw new Exception($"Failed to fetch match stats. Status code: {response.StatusCode}");
                    }
                }

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new Exception("Empty response received from the server.");
                }

                var json = JObject.Parse(content);
                var rounds = json["rounds"] as JArray;

                if (rounds == null || rounds.Count == 0)
                {
                    throw new Exception("No match data found");
                }

                var firstRound = rounds[0];
                var teams = firstRound["teams"] as JArray;

                if (teams == null || teams.Count != 2)
                {
                    throw new Exception("Invalid team data");
                }

                var match = new Match
                {
                    MatchId = matchId,
                    Map = firstRound["round_stats"]?["Map"]?.ToString(),
                    Team1 = ProcessTeam(teams[0]),
                    Team2 = ProcessTeam(teams[1])
                };

                return match;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching match stats: {ex.Message}", ex);
            }
        }

        private MatchTeam ProcessTeam(JToken teamToken)
        {
            var team = new MatchTeam
            {
                Name = teamToken["team_stats"]?["Team"]?.ToString(),
                Score = int.Parse(teamToken["team_stats"]?["Final Score"]?.ToString() ?? "0"),
                Players = new List<MatchPlayer>()
            };

            var players = teamToken["players"] as JArray;
            if (players != null)
            {
                foreach (var playerToken in players)
                {
                    var stats = playerToken["player_stats"];
                    var player = new MatchPlayer
                    {
                        Nickname = playerToken["nickname"]?.ToString(),
                        AvatarUrl = playerToken["avatar"]?.ToString(),

                        // Core stats
                        Kills = ParseInt(stats?["Kills"]),
                        Deaths = ParseInt(stats?["Deaths"]),
                        Assists = ParseInt(stats?["Assists"]),
                        KDRatio = ParseDouble(stats?["K/D Ratio"]),
                        ADR = ParseDouble(stats?["ADR"]),

                        // Special kills
                        DoubleKills = ParseInt(stats?["Double Kills"]),
                        PistolKills = ParseInt(stats?["Pistol Kills"]),
                        SniperKills = ParseInt(stats?["Sniper Kills"]),

                        // Entry stats
                        EntryCount = ParseInt(stats?["Entry Count"]),
                        EntryWins = ParseInt(stats?["Entry Wins"]),
                        EntrySuccessRate = ParseDouble(stats?["Match Entry Success Rate"])*100,

                        // Utility and flash stats
                        UtilityDamage = ParseDouble(stats?["Utility Damage"]),
                        FlashCount = ParseInt(stats?["Flash Count"]),

                        // Headshot stats
                        Headshots = ParseInt(stats?["Headshots"]),
                        HeadshotPercentage = ParseDouble(stats?["Headshots %"]),

                        // Clutch stats
                        OneVOneWins = ParseInt(stats?["1v1Wins"]),

                        // Damage stats
                        Damage = ParseInt(stats?["Damage"])
                    };

                    team.Players.Add(player);
                }
            }

            return team;
        }

        private int ParseInt(JToken token)
        {
            if (token == null) return 0;
            return int.TryParse(token.ToString(), out int result) ? result : 0;
        }

        private double ParseDouble(JToken token)
        {
            if (token == null) return 0.0;
            var value = token.ToString().Replace("%", "");
            return double.TryParse(value, out double result) ? result : 0.0;
        }
    }
}