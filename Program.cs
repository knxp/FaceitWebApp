using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using faceitApp.Handlers;
using faceitApp.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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

builder.Services.AddHttpClient<GetTeamID>(client =>
{
    client.BaseAddress = new Uri("https://open.faceit.com/");
});
builder.Services.AddTransient<GetTeamID>();

builder.Services.AddSingleton<IConfiguration>(builder.Configuration); // Register configuration

var app = builder.Build();

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
