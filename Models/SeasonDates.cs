public class SeasonDates
{
    public static readonly Dictionary<string, (long Start, long End)> Seasons = new()
    {
        { "ESEA S51", (1728190800, 1734328800) },
        { "ESEA S52", (1736748000, 1742706000) },
        { "ESEA S53", (1743897600, 1747267200) },
        { "ESEA S54", (1752353035, 1758487435) }
    };

    public static readonly Dictionary<string, string[]> MapPools = new()
    {
        { "ESEA S51", new[] { "Ancient", "Anubis", "Inferno", "Mirage", "Nuke", "Vertigo", "Dust2" } },
        { "ESEA S52", new[] { "Ancient", "Anubis", "Inferno", "Mirage", "Nuke", "Train", "Dust2" } },
        { "ESEA S53", new[] { "Ancient", "Anubis", "Inferno", "Mirage", "Nuke", "Train", "Dust2" } },
        { "ESEA S54", new[] { "Ancient", "Overpass", "Inferno", "Mirage", "Nuke", "Train", "Dust2" } }
    };

    public static bool IsMatchInSeason(long matchTimestamp, string competitionName)
    {
        foreach (var (seasonName, (start, end)) in Seasons)
        {
            if (matchTimestamp >= start && matchTimestamp <= end &&
                competitionName.Contains(seasonName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}