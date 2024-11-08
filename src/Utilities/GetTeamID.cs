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
        private readonly Regex _teamIdPattern = new Regex(@"teams/([a-fA-F0-9-]{36})", RegexOptions.IgnoreCase);

        public GetTeamID(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _faceitApiKey = configuration["Faceit:ApiKey"];
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

                string teamId = null;

                // Check if input is a URL containing a team ID
                if (input.Contains("faceit.com/"))
                {
                    var match = _teamIdPattern.Match(input);
                    if (match.Success)
                    {
                        teamId = match.Groups[1].Value;
                    }
                }
                // If input looks like a team ID itself
                else if (Regex.IsMatch(input, @"^[a-fA-F0-9-]{36}$"))
                {
                    teamId = input;
                }

                // If we found a team ID, validate it plays CS2
                if (!string.IsNullOrEmpty(teamId))
                {
                    var validationResult = await ValidateTeamId(teamId);
                    if (!string.IsNullOrEmpty(validationResult.TeamId))
                    {
                        return validationResult;
                    }
                }

                // If no team ID found or validation failed, try searching by nickname
                if (teamId == null)
                {
                    return await SearchTeamByNickname(input);
                }

                return (null, "Team not found or does not play CS2");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<(string TeamId, string ErrorMessage)> ValidateTeamId(string teamId)
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

        private async Task<(string TeamId, string ErrorMessage)> SearchTeamByNickname(string nickname)
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
    }
}