using Microsoft.Extensions.Configuration;

namespace faceitWebApp.Services;

public interface IConfigurationService
{
    string GetFaceitApiKey();
    string GetGameId();
}

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetFaceitApiKey()
    {
        var apiKey = _configuration["faceitapikey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("faceitapikey not found in configuration");
        }
        return apiKey;
    }

    public string GetGameId()
    {
        return _configuration["Faceit:gameId"] ?? "cs2";
    }
}