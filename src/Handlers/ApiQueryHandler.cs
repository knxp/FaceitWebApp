using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace faceitApp.Handlers
{
    public class ApiQueryHandler
    {
        private readonly HttpClient _httpClient;

        public ApiQueryHandler(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetDataAsync()
        {
            string faceitApiKey = "ec10d6d5-907d-43f7-bba2-9bf071392303"; // Hardcoded API key
            string url = $"https://open.faceit.com/data/v4/players/6b99b73f-2f7c-4e0d-b4a8-c7da09cfb4c2"; // Ensure this is correct

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + faceitApiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            Console.WriteLine($"Request URL: {url}");
            Console.WriteLine($"Authorization: Bearer {faceitApiKey}");

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                return jsonResponse; // Return the raw JSON response
            }
            else
            {
                string error = $"Failed to query API. Status code: {response.StatusCode}";
                Console.WriteLine(error);
                return error;
            }
        }
    }
}
