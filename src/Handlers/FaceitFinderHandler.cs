using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using faceitWebApp.Models;

namespace faceitWebApp.Handlers
{
    public class FaceitFinderHandler
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FaceitFinderHandler(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<(FaceitFinderPlayer player, string error)> SearchPlayerAsync(string steamUrl)
        {
            try
            {
                var steamId = await GetSteamId64FromUrl(steamUrl);
                if (string.IsNullOrEmpty(steamId))
                {
                    return (null, "Could not find Steam ID");
                }

                var player = await SearchFaceitPlayer(steamId);
                if (player == null)
                {
                    return (null, "No FACEIT account found for this Steam profile");
                }

                return (player, null);
            }
            catch (Exception ex)
            {
                return (null, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<string> GetSteamId64FromUrl(string url)
        {
            try
            {
                // If it's already a Steam64 ID
                if (url.Contains("/profiles/"))
                {
                    return url.Split("/profiles/")[1].TrimEnd('/');
                }

                // If it's a vanity URL
                if (url.Contains("/id/"))
                {
                    var vanityUrl = url.Split("/id/")[1].TrimEnd('/');
                    var response = await _httpClient.GetAsync($"https://steamcommunity.com/id/{vanityUrl}?xml=1");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        if (content.Contains("<steamID64>"))
                        {
                            var steamId = content.Split("<steamID64>")[1].Split("</steamID64>")[0];
                            return steamId;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string ExtractVanityUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments;
                return segments.Last().TrimEnd('/');
            }
            catch
            {
                return url; // If it's not a URL, try using the input directly
            }
        }

        private async Task<FaceitFinderPlayer> SearchFaceitPlayer(string steamId64)
        {
            var faceitApiKey = _configuration["faceitapikey"];
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://open.faceit.com/data/v4/players?game_player_id={steamId64}&game=cs2");
            request.Headers.Add("Authorization", $"Bearer {faceitApiKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            return JsonSerializer.Deserialize<FaceitFinderPlayer>(content, options);
        }
    }
}