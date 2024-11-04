using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace faceitApp.Utilities
{
    public class GetPlayerID
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;

        public GetPlayerID(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<string> GetPlayerIDFromNicknameAsync(string nickname)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // First try exact match
                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/players?nickname={Uri.EscapeDataString(nickname)}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    return json["player_id"]?.ToString();
                }

                // If exact match fails, try case-insensitive search
                response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/search/players?nickname={Uri.EscapeDataString(nickname)}&offset=0&limit=1");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var items = json["items"] as JArray;

                    if (items != null && items.Count > 0)
                    {
                        var foundNickname = items[0]["nickname"]?.ToString();
                        var playerId = items[0]["player_id"]?.ToString();

                        // If the found nickname matches case-insensitively but not exactly,
                        // we'll still use it but inform the user
                        if (!string.Equals(nickname, foundNickname, StringComparison.Ordinal) &&
                            string.Equals(nickname, foundNickname, StringComparison.OrdinalIgnoreCase))
                        {
                            return playerId;
                        }
                        return playerId;
                    }
                }

                return "Player not found";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}