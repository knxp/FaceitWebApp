using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
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
using Blazor.Analytics;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Configure Google Analytics
var googleAnalyticsTrackingId = builder.Configuration["GoogleAnalytics:TrackingId"];
builder.Services.AddGoogleAnalytics(googleAnalyticsTrackingId);


// Add Blazorise
builder.Services
    .AddBlazorise()
    .AddBootstrapProviders()
    .AddFontAwesomeIcons();

// Configure base HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configure HttpClient for FACEIT API with proper base URL
void ConfigureFaceitHttpClient(HttpClient client)
{
    client.BaseAddress = new Uri("https://open.faceit.com/data/v4/");
    client.DefaultRequestHeaders.Add("accept", "application/json");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["Faceit:ApiKey"]}");
}

// Register HTTP clients with proper configuration
builder.Services.AddHttpClient<MatchStatsHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<GetMatchHistory>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<BasicStatsHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<FullStatsHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<PlayerLeagueStatsHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<TeamStatsHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<GetPlayerID>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<GetPlayerInfo>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<GetTeamID>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<ActivePlayersHandler>(client => ConfigureFaceitHttpClient(client));
builder.Services.AddHttpClient<SeasonStatsHandler>(client => ConfigureFaceitHttpClient(client));

// Register services as transient
builder.Services.AddTransient<GetMatchHistory>();
builder.Services.AddTransient<BasicStatsHandler>();
builder.Services.AddTransient<FullStatsHandler>();
builder.Services.AddTransient<PlayerLeagueStatsHandler>();
builder.Services.AddTransient<TeamStatsHandler>();
builder.Services.AddTransient<GetPlayerID>();
builder.Services.AddTransient<GetPlayerInfo>();
builder.Services.AddTransient<GetTeamID>();
builder.Services.AddTransient<MatchStatsHandler>();
builder.Services.AddTransient<ActivePlayersHandler>();
builder.Services.AddTransient<SeasonStatsHandler>();

await builder.Build().RunAsync();