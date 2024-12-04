using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using faceitWebApp.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

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

        private string ExtractMatchId(string input)
        {
            // Remove any whitespace and handle null input
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = input.Trim();

            // If it's a URL, extract the match ID
            if (input.Contains("faceit.com") || input.Contains("room/"))
            {
                // Find the part after "room/" and before the next "/" or end of string
                var roomIndex = input.IndexOf("room/");
                if (roomIndex != -1)
                {
                    var startIndex = roomIndex + 5; // Length of "room/"
                    var endIndex = input.IndexOf('/', startIndex);
                    var matchId = endIndex != -1
                        ? input.Substring(startIndex, endIndex - startIndex)
                        : input.Substring(startIndex);

                    // Remove any query parameters if present
                    var queryIndex = matchId.IndexOf('?');
                    if (queryIndex != -1)
                    {
                        matchId = matchId.Substring(0, queryIndex);
                    }

                    return matchId;
                }
            }

            // If it's already in the correct format (1-uuid), return as is
            if (input.StartsWith("1-"))
            {
                return input;
            }

            // If it's just the UUID part, add the "1-" prefix
            if (Guid.TryParse(input, out _))
            {
                return $"1-{input}";
            }

            // For any other format, add the "1-" prefix if not present
            return input.StartsWith("1-") ? input : $"1-{input}";
        }

        private async Task<string> GetTeamAvatar(string teamId)
        {
            try
            {
                // Ensure authorization header is set
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var teamData = JObject.Parse(content);
                    var avatar = teamData["avatar"]?.ToString();
                    return !string.IsNullOrEmpty(avatar) ? avatar : "/images/defaultTeam.webp";
                }
            }
            catch
            {
                // Log error if needed
            }
            return "/images/defaultTeam.webp";
        }

        public async Task<Models.Match> GetMatchStatsAsync(string input)
        {
            try
            {
                var matchId = ExtractMatchId(input);

                if (string.IsNullOrWhiteSpace(matchId))
                {
                    throw new Exception("Invalid match ID format");
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

                var totalRounds = firstRound["round_stats"]?["Rounds"]?.ToObject<double>() ?? 0;
                var match = new Models.Match
                {
                    MatchId = matchId,
                    Map = firstRound["round_stats"]?["Map"]?.ToString(),
                    TotalRounds = totalRounds,
                    Team1 = await ProcessTeam(teams[0], totalRounds),
                    Team2 = await ProcessTeam(teams[1], totalRounds)
                };

                return match;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching match stats: {ex.Message}", ex);
            }
        }

        private async Task<MatchTeam> ProcessTeam(JToken teamToken, double totalRounds)
        {
            var teamId = teamToken["team_id"]?.ToString();
            var avatar = !string.IsNullOrEmpty(teamId)
                ? await GetTeamAvatar(teamId)
                : "/images/defaultTeam.webp";


            var team = new MatchTeam
            {
                TeamId = teamId,
                Name = teamToken["team_stats"]?["Team"]?.ToString(),
                Score = int.Parse(teamToken["team_stats"]?["Final Score"]?.ToString() ?? "0"),
                Avatar = avatar,
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
                        TripleKills = ParseInt(stats?["Triple Kills"]),
                        QuadKills = ParseInt(stats?["Quadro Kills"]),
                        Aces = ParseInt(stats?["Penta Kills"]),
                        PistolKills = ParseInt(stats?["Pistol Kills"]),
                        SniperKills = ParseInt(stats?["Sniper Kills"]),

                        // Entry stats
                        EntryCount = ParseInt(stats?["Entry Count"]),
                        EntryWins = ParseInt(stats?["Entry Wins"]),
                        EntrySuccessRate = ParseDouble(stats?["Match Entry Success Rate"]) * 100,

                        // Utility and flash stats
                        UtilityDamage = ParseDouble(stats?["Utility Damage"]),
                        FlashCount = ParseInt(stats?["Flash Count"]),
                        FlashSuccesses = ParseInt(stats?["Flash Successes"]),

                        // Headshot stats
                        Headshots = ParseInt(stats?["Headshots"]),
                        HeadshotPercentage = ParseDouble(stats?["Headshots %"]),

                        // Clutch stats
                        OneVOneCount = ParseInt(stats?["1v1Count"]),
                        OneVOneWins = ParseInt(stats?["1v1Wins"]),
                        OneVTwoCount = ParseInt(stats?["1v2Count"]),
                        OneVTwoWins = ParseInt(stats?["1v2Wins"]),

                        // Damage stats
                        Damage = ParseInt(stats?["Damage"]),
                    };

                    // Calculate rating directly using the MatchPlayer
                    // Need to convert MatchPlayer to Player since CalculateRating expects a Player object
                    var playerForRating = new Player
                    {
                        
                        Kills = player.Kills,
                        Deaths = player.Deaths, 
                        Assists = player.Assists,
                        ADR = player.ADR,
                        KDRatio = player.KDRatio,
                        DoubleKills = player.DoubleKills,
                        TripleKills = player.TripleKills,
                        QuadroKills = player.QuadKills,
                        PentaKills = player.Aces
                    };
                    player.Rating = new RatingHandler().CalculateRating(playerForRating, totalRounds, 1);

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
