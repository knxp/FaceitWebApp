using System;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace faceitWebApp.Services
{
    public class ApiKeyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string? _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public ApiKeyService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://maxfaceitstats-api.azurewebsites.net";
        }

        public async Task<string> GetApiKeysToken()
        {
            // Check if we have a valid cached token
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            // Request new token
            var response = await _httpClient.PostAsync(
                $"{_apiBaseUrl}/api/token",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to get API keys token");
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            _cachedToken = result?.Token;
            _tokenExpiry = DateTime.UtcNow.AddMinutes(55); // Refresh 5 minutes before expiry

            return _cachedToken ?? throw new Exception("No token received");
        }

        private class TokenResponse
        {
            public string? Token { get; set; }
        }
    }
}