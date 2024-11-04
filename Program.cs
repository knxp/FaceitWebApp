using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using faceitWebApp.Handlers;
using faceitWebApp.Utilities;
using faceitWebApp.Services;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using faceitWebApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Add Blazorise
builder.Services
    .AddBlazorise()
    .AddBootstrapProviders()
    .AddFontAwesomeIcons();

// Configure base HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configure HttpClient for FACEIT API
void ConfigureHttpClient(HttpClient client)
{
    client.BaseAddress = new Uri("https://open.faceit.com/data/v4/");
    client.DefaultRequestHeaders.Add("accept", "application/json");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["Faceit:ApiKey"]}");
}

// Register HTTP clients with proper configuration
builder.Services.AddHttpClient<GetMatchHistory>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<BasicStatsHandler>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<FullStatsHandler>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<PlayerLeagueStatsHandler>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<TeamStatsHandler>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<GetPlayerID>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<GetPlayerInfo>(client => ConfigureHttpClient(client));
builder.Services.AddHttpClient<GetTeamID>(client => ConfigureHttpClient(client));

// Register services as transient
builder.Services.AddTransient<GetMatchHistory>();
builder.Services.AddTransient<BasicStatsHandler>();
builder.Services.AddTransient<FullStatsHandler>();
builder.Services.AddTransient<PlayerLeagueStatsHandler>();
builder.Services.AddTransient<TeamStatsHandler>();
builder.Services.AddTransient<GetPlayerID>();
builder.Services.AddTransient<GetPlayerInfo>();
builder.Services.AddTransient<GetTeamID>();

await builder.Build().RunAsync();