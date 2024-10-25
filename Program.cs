using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using faceitApp.Handlers;
using faceitApp.Utilities;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add Blazorise
builder.Services
    .AddBlazorise()
    .AddBootstrapProviders()
    .AddFontAwesomeIcons();

// Configure HttpClient for GetMatchHistory with proper base address
builder.Services.AddHttpClient<GetMatchHistory>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});

// Other service registrations...
builder.Services.AddHttpClient<BasicStatsHandler>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<BasicStatsHandler>();

builder.Services.AddHttpClient<FullStatsHandler>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<FullStatsHandler>();

builder.Services.AddHttpClient<PlayerLeagueStatsHandler>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<PlayerLeagueStatsHandler>();

builder.Services.AddHttpClient<TeamStatsHandler>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<TeamStatsHandler>();

builder.Services.AddHttpClient<GetPlayerID>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<GetPlayerID>();

builder.Services.AddHttpClient<GetPlayerInfo>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<GetPlayerInfo>();

builder.Services.AddHttpClient<GetTeamID>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<GetTeamID>();

// Change GetMatchHistory to Transient for better consistency
builder.Services.AddTransient<GetMatchHistory>();

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();