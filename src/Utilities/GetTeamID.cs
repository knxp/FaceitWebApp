using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace faceitWebApp.Utilities
{
    public class GetTeamID
    {
        private readonly HttpClient _httpClient;
        private readonly string _faceitApiKey;
        private readonly Regex _teamIdPattern = new Regex(@"teams/([a-fA-F0-9-]+)", RegexOptions.IgnoreCase);

        public GetTeamID(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["faceitapikey"];
        }

        public async Task<(string TeamId, string ErrorMessage)> GetTeamIDFromUrlAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (null, "Input is empty");
            }

            try
            {
                // Decode URL if it's encoded
                input = Uri.UnescapeDataString(input);

                // Check if input is a URL containing a team ID
                if (input.Contains("faceit.com/") || input.Contains("teams/"))
                {
                    var match = _teamIdPattern.Match(input);
                    if (match.Success)
                    {
                        var teamId = match.Groups[1].Value;
                        return await ValidateTeamId(teamId);
                    }
                }
                // If input looks like a team ID itself
                else if (Regex.IsMatch(input, @"^[a-fA-F0-9-]+$"))
                {
                    return await ValidateTeamId(input);
                }

                // If no team ID found, try searching by nickname
                return await SearchTeamByNickname(input);
            }
            catch (Exception ex)
            {
                return (null, $"Error processing input: {ex.Message}");
            }
        }

        private async Task<(string TeamId, string ErrorMessage)> ValidateTeamId(string teamId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/teams/{teamId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);

                    if (json["game"]?.ToString().Equals("cs2", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return (teamId, null);
                    }
                    return (null, "Team does not play CS2");
                }
                return (null, "Team not found");
            }
            catch (Exception ex)
            {
                return (null, $"Error validating team: {ex.Message}");
            }
        }

        private async Task<(string TeamId, string ErrorMessage)> SearchTeamByNickname(string nickname)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _faceitApiKey);

                var response = await _httpClient.GetAsync($"https://open.faceit.com/data/v4/search/teams?nickname={Uri.EscapeDataString(nickname)}&game=cs2&offset=0&limit=1");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var items = json["items"] as JArray;

                    if (items != null && items.Count > 0)
                    {
                        var team = items[0];
                        var teamId = team["team_id"]?.ToString();

                        if (!string.IsNullOrEmpty(teamId))
                        {
                            return await ValidateTeamId(teamId);
                        }
                    }
                }

                return (null, "Team not found");
            }
            catch (Exception ex)
            {
                return (null, $"Error searching team: {ex.Message}");
            }
        }
    }
}
