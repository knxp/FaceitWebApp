using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using faceitWebApp.Models;
using System.Collections.Generic;

namespace faceitWebApp.Utilities
{
    public class GetPlayerInfo
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly FaceitService _faceitService;

        public GetPlayerInfo(HttpClient httpClient, IConfiguration configuration, FaceitService faceitService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _faceitService = faceitService;
        }

        private async Task<string> GetFaceitApiKey()
        {
            return await _faceitService.GetFaceitApiKey();
        }

        public async Task<Player> GetPlayerInfoAsync(string playerId, string input)
        {
            try
            {
                var faceitApiKey = await GetFaceitApiKey();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + faceitApiKey);

                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players/{playerId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var player = new Player
                {
                    Id = playerId,
                    Nickname = json["nickname"]?.ToString(),
                    Avatar = json["avatar"]?.ToString(),
                    Teams = new List<TeamInfo>()
                };

                // Get CS2 game info
                var games = json["games"]?["cs2"];
                if (games != null)
                {
                    player.Level = games["skill_level"]?.Value<int>();
                    player.Elo = games["faceit_elo"]?.Value<int>();
                    player.Region = games["region"]?.ToString();

                    // Get region rank if region is available
                    if (!string.IsNullOrEmpty(player.Region))
                    {
                        var rankResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/rankings/games/cs2/regions/{player.Region}/players/{playerId}");
                        if (rankResponse.IsSuccessStatusCode)
                        {
                            var rankContent = await rankResponse.Content.ReadAsStringAsync();
                            var rankJson = JObject.Parse(rankContent);
                            player.regionRank = rankJson["position"]?.Value<int>();
                        }
                    }
                }

                // Get teams
                var teamsResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players/{playerId}/teams");
                if (teamsResponse.IsSuccessStatusCode)
                {
                    var teamsContent = await teamsResponse.Content.ReadAsStringAsync();
                    var teamsJson = JObject.Parse(teamsContent);
                    var teams = teamsJson["items"] as JArray;

                    if (teams != null)
                    {
                        foreach (var team in teams)
                        {
                            if (team["game"]?.ToString() == "cs2")
                            {
                                player.Teams.Add(new TeamInfo
                                {
                                    Name = team["name"]?.ToString(),
                                    Avatar = team["avatar"]?.ToString()
                                });
                            }
                        }
                    }
                }

                return player;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching player info: {ex.Message}");
            }
        }
    }
}