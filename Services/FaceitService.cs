using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using faceitWebApp.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

public class FaceitService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ApiKeyService _apiKeyService;

    public FaceitService(HttpClient httpClient, IConfiguration configuration, ApiKeyService apiKeyService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _apiKeyService = apiKeyService;
    }

    public async Task<string> GetFaceitApiKey()
    {
        if (_configuration.GetValue<bool>("UseLocalKeys"))
        {
            return _configuration["faceitapikey"];
        }

        var token = await _apiKeyService.GetApiKeysToken();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims.First(c => c.Type == "FaceitApiKey").Value;
    }
}