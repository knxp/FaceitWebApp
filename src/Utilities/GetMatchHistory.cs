using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace faceitWebApp.Utilities
{
    public class GetMatchHistory
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;

        public GetMatchHistory(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<List<MatchHistory>> GetMatchHistoryAsync(string playerId)
        {
            var matches = new List<MatchHistory>();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

            var url = $"https://open.faceit.com/data/v4/players/{playerId}/history?game=cs2&offset=0&limit=20";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var items = json["items"] as JArray;

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        var teams = item["teams"] as JObject;
                        if (teams != null)
                        {
                            var faction1 = teams["faction1"];
                            var faction2 = teams["faction2"];
                            var playerTeam = "";

                            // Find which team the player was on
                            if (faction1["players"] != null)
                            {
                                foreach (var player in faction1["players"])
                                {
                                    if (player["player_id"].ToString() == playerId)
                                    {
                                        playerTeam = "faction1";
                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(playerTeam) && faction2["players"] != null)
                            {
                                foreach (var player in faction2["players"])
                                {
                                    if (player["player_id"].ToString() == playerId)
                                    {
                                        playerTeam = "faction2";
                                        break;
                                    }
                                }
                            }

                            // Determine if the player won
                            var winner = item["results"]?["winner"]?.ToString();
                            var result = playerTeam == winner ? 1 : 0;

                            matches.Add(new MatchHistory
                            {
                                MatchId = item["match_id"].ToString(),
                                PlayerId = playerId,
                                Result = result
                            });
                        }
                    }
                }
            }

            return matches;
        }
    }

    public class MatchHistory
    {
        public string MatchId { get; set; }
        public string PlayerId { get; set; }
        public int Result { get; set; }
    }
}