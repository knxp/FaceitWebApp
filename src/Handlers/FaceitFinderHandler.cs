using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using faceitWebApp.Models;
using faceitWebApp.Services;

namespace faceitWebApp.Handlers
{
    public class FaceitFinderHandler
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly FaceitService _faceitService;
        private readonly ApiKeyService _apiKeyService;

        public FaceitFinderHandler(HttpClient httpClient, IConfiguration configuration, FaceitService faceitService, ApiKeyService apiKeyService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _faceitService = faceitService;
            _apiKeyService = apiKeyService;
        }

        private async Task<string> GetFaceitApiKey()
        {
            return await _faceitService.GetFaceitApiKey();
        }

        public async Task<(FaceitFinderPlayer player, string error)> SearchPlayerAsync(string steamUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(steamUrl))
                {
                    return (null, "Please enter a Steam profile URL");
                }

                // Clean up the URL if needed
                steamUrl = steamUrl.TrimEnd('/');

                // Get Steam64 ID from URL
                var steamId64 = await GetSteamId64FromUrl(steamUrl);
                if (string.IsNullOrEmpty(steamId64))
                {
                    return (null, "Could not find Steam ID from the provided URL");
                }

                // Search FACEIT using Steam64 ID
                var player = await SearchFaceitPlayer(steamId64);
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

                    // Add Accept header to specify we want JSON
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await _httpClient.GetAsync($"https://maxfaceitstats-api-c4b8dudcgsdxeraf.centralus-01.azurewebsites.net/api/steam/resolve/{vanityUrl}");


                    if (response.IsSuccessStatusCode)
                    {
                        var steamId = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"API Response Content: {steamId}");
                        steamId = steamId.Trim('"');

                        // Validate that we got a proper Steam64 ID (17 digits)
                        if (!string.IsNullOrEmpty(steamId) && steamId.Length == 17 && long.TryParse(steamId, out _))
                        {
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
            try
            {
                var faceitApiKey = await _faceitService.GetFaceitApiKey();

                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://open.faceit.com/data/v4/players?game_player_id={steamId64}&game=cs2");

                request.Headers.Add("Authorization", $"Bearer {faceitApiKey}");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);

                if (jsonResponse.TryGetProperty("games", out var games) &&
                    games.TryGetProperty("cs2", out var cs2Stats))
                {
                    var hasEseaMembership = jsonResponse.TryGetProperty("memberships", out var memberships) &&
                        memberships.EnumerateArray().Any(m => m.GetString() == "esea");

                    var player = new FaceitFinderPlayer
                    {
                        PlayerId = jsonResponse.GetProperty("player_id").GetString(),
                        Nickname = jsonResponse.GetProperty("nickname").GetString(),
                        Avatar = jsonResponse.GetProperty("avatar").GetString(),
                        Country = jsonResponse.GetProperty("country").GetString(),
                        Region = jsonResponse.GetProperty("games")
                            .GetProperty("cs2")
                            .GetProperty("region").GetString(),
                        SkillLevel = jsonResponse.GetProperty("games")
                            .GetProperty("cs2")
                            .GetProperty("skill_level").GetInt32(),
                        FaceitElo = jsonResponse.GetProperty("games")
                            .GetProperty("cs2")
                            .GetProperty("faceit_elo").GetInt32(),
                        ProfileURL = jsonResponse.GetProperty("faceit_url").GetString().Replace("{lang}", "en"),
                        HasEseaMembership = hasEseaMembership,
                        AccountCreated = jsonResponse.TryGetProperty("activated_at", out var activatedAt)
                            ? DateTime.Parse(activatedAt.GetString())
                            : DateTime.MinValue
                    };

                    // After creating the player object, fetch their CS2 stats
                    using var statsRequest = new HttpRequestMessage(HttpMethod.Get,
                        $"https://open.faceit.com/data/v4/players/{player.PlayerId}/stats/cs2");
                    statsRequest.Headers.Add("Authorization", $"Bearer {faceitApiKey}");

                    var statsResponse = await _httpClient.SendAsync(statsRequest);

                    if (statsResponse.IsSuccessStatusCode)
                    {
                        var statsContent = await statsResponse.Content.ReadAsStringAsync();
                        var statsJson = JsonSerializer.Deserialize<JsonElement>(statsContent);

                        if (statsJson.TryGetProperty("lifetime", out var lifetime))
                        {
                            if (lifetime.TryGetProperty("Matches", out var matches))
                            {
                                player.TotalMatches = int.Parse(matches.GetString());
                            }
                            if (lifetime.TryGetProperty("ADR", out var adr))
                            {
                                player.AverageDamagePerRound = double.Parse(adr.GetString());
                            }
                            if (lifetime.TryGetProperty("Average Headshots %", out var hs))
                            {
                                player.HeadshotPercentage = double.Parse(hs.GetString());
                            }
                        }
                    }

                    // Get player's match history
                    using var historyRequest = new HttpRequestMessage(HttpMethod.Get,
                        $"https://open.faceit.com/data/v4/players/{player.PlayerId}/history?game=cs2&offset=0&limit=50");
                    historyRequest.Headers.Add("Authorization", "Bearer " + faceitApiKey);

                    var historyResponse = await _httpClient.SendAsync(historyRequest);

                    if (historyResponse.IsSuccessStatusCode)
                    {
                        var historyContent = await historyResponse.Content.ReadAsStringAsync();
                        var historyJson = JsonSerializer.Deserialize<JsonElement>(historyContent);

                        if (historyJson.TryGetProperty("items", out var matches))
                        {
                            // Set latest match date from the first match
                            if (matches.GetArrayLength() > 0)
                            {
                                var firstMatch = matches[0];
                                if (firstMatch.TryGetProperty("started_at", out var startedAt))
                                {
                                    // Convert Unix timestamp to UTC then to local time
                                    var utcDate = DateTimeOffset.FromUnixTimeSeconds(startedAt.GetInt64()).UtcDateTime;
                                    player.LatestMatchDate = utcDate.ToLocalTime();
                                }

                                // Only look for team info if they have ESEA membership
                                if (hasEseaMembership)
                                {
                                    // Look through matches for ESEA matches
                                    foreach (var match in matches.EnumerateArray())
                                    {
                                        if (match.TryGetProperty("competition_name", out var competitionName) &&
                                            competitionName.GetString().StartsWith("ESEA") &&
                                            match.TryGetProperty("teams", out var teams))
                                        {
                                            // Check faction1
                                            if (teams.TryGetProperty("faction1", out var faction1))
                                            {
                                                var players = faction1.GetProperty("players").EnumerateArray();
                                                if (players.Any(p => p.GetProperty("player_id").GetString() == player.PlayerId))
                                                {
                                                    player.TeamId = faction1.GetProperty("team_id").GetString();
                                                    player.TeamName = faction1.GetProperty("nickname").GetString();

                                                    // Extract division from competition name
                                                    var compName = competitionName.GetString();
                                                    var parts = compName.Split(' ');
                                                    for (int i = 0; i < parts.Length; i++)
                                                    {
                                                        if (parts[i] == "Open1-8" || parts[i] == "Open9-10" || parts[i] == "Intermediate" ||
                                                            parts[i] == "Main" || parts[i] == "Advanced" ||
                                                            parts[i] == "ECL")
                                                        {
                                                            player.TeamDivision = parts[i];
                                                            goto FoundTeam;
                                                        }
                                                    }
                                                }
                                            }

                                            // Check faction2
                                            if (teams.TryGetProperty("faction2", out var faction2))
                                            {
                                                var players = faction2.GetProperty("players").EnumerateArray();
                                                if (players.Any(p => p.GetProperty("player_id").GetString() == player.PlayerId))
                                                {
                                                    player.TeamId = faction2.GetProperty("team_id").GetString();
                                                    player.TeamName = faction2.GetProperty("nickname").GetString();

                                                    // Extract division from competition name
                                                    var compName = competitionName.GetString();
                                                    var parts = compName.Split(' ');
                                                    for (int i = 0; i < parts.Length; i++)
                                                    {
                                                        if (parts[i] == "Open1-8" || parts[i] == "Open9-10" || parts[i] == "Intermediate" ||
                                                            parts[i] == "Main" || parts[i] == "Advanced" ||
                                                            parts[i] == "ECL")
                                                        {
                                                            player.TeamDivision = parts[i];
                                                            goto FoundTeam;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                FoundTeam:
                    return player;
                }
                else
                {
                    Console.WriteLine("Could not find CS2 stats in FACEIT response"); // Debug log
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}