using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using faceitWebApp.Services;

namespace faceitWebApp.Utilities
{
    public class GetPlayerID
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly FaceitService _faceitService;

        public GetPlayerID(HttpClient httpClient, IConfiguration configuration, FaceitService faceitService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _faceitService = faceitService;
        }

        private async Task<string> GetFaceitApiKey()
        {
            return await _faceitService.GetFaceitApiKey();
        }

        public async Task<string> GetPlayerIDFromNicknameAsync(string nickname)
        {
            try
            {
                var faceitApiKey = await GetFaceitApiKey();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + faceitApiKey);

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