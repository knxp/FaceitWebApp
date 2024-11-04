using System.Collections.Generic;

namespace faceitApp.Dictionaries
{
    public static class BasicStatDictionary
{
    public static readonly Dictionary<string, double> Stats = new Dictionary<string, double>
    {
        { "Kills", 0 },
        { "Assists", 0 },
        { "Deaths", 0 },
        { "K/D Ratio", 0 },
        { "K/R Ratio", 0 },
        { "Headshots", 0 },
        { "Headshots %", 0 },
        { "Triple Kills", 0 },
        { "Quadro Kills", 0 },
        { "Penta Kills", 0 },
        { "MVPs", 0 }
    };

    public static readonly List<string> CustomOrder = new List<string>
    {
        "Kills",
        "Assists",
        "Deaths",
        "K/D Ratio",
        "K/R Ratio",
        "Headshots",
        "Headshots %",
        "Triple Kills",
        "Quadro Kills",
        "Penta Kills",
        "MVPs"
    };
}
}