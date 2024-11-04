using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace faceitWebApp.Utilities
{
    public class GetTeamID
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;

        public GetTeamID(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
        }

        public async Task<string> GetTeamIDFromUrlAsync(string input)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                // Check if input is a URL
                if (input.Contains("faceit.com/") || input.Contains("/teams/"))
                {
                    // Extract team name from URL
                    var segments = input.Split(new[] { "/teams/", "/team/" }, StringSplitOptions.None);
                    if (segments.Length > 1)
                    {
                        var teamId = segments[1].Split('/')[0];
                        var teamResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");

                        if (teamResponse.IsSuccessStatusCode)
                        {
                            var content = await teamResponse.Content.ReadAsStringAsync();
                            var json = JObject.Parse(content);
                            var games = json["games"] as JArray;

                            if (games != null && games.Any(g => g["name"]?.ToString().Equals("cs2", StringComparison.OrdinalIgnoreCase) == true))
                            {
                                return json["team_id"]?.ToString();
                            }
                        }
                    }
                }

                // If not a URL or URL lookup failed, search by name
                var searchResponse = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/search/teams?nickname={Uri.EscapeDataString(input)}&game=cs2&offset=0&limit=50");

                if (searchResponse.IsSuccessStatusCode)
                {
                    var content = await searchResponse.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var items = json["items"] as JArray;

                    if (items != null && items.Count > 0)
                    {
                        // Find exact name match
                        var exactMatch = items.FirstOrDefault(item =>
                            string.Equals(item["name"]?.ToString(), input, StringComparison.Ordinal));

                        if (exactMatch != null)
                        {
                            return exactMatch["team_id"]?.ToString();
                        }
                    }
                }

                return "Failed: Team not found. Please check the exact team name (case sensitive) or URL and ensure it plays CS2";
            }
            catch (Exception ex)
            {
                return $"Failed: {ex.Message}";
            }
        }
    }
}