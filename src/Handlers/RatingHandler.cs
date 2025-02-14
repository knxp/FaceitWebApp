using faceitWebApp.Models;
using System;

namespace faceitWebApp.Handlers
{
    public class RatingHandler
    {
        private double CalculateImpact(Player player, double avgRounds)
        {
            
            double kpr = player.Kills / avgRounds;
            double dpr = player.Deaths / avgRounds;
            double apr = player.Assists / avgRounds;
            double multiKills = player.DoubleKills + player.TripleKills + player.QuadroKills + player.PentaKills;
            double multiKillsPerRound = multiKills / avgRounds;
            double clutchKillsPerRound = player.ClutchKills / avgRounds;

            double impact = 2.13 * kpr + 0.42 * apr + -0.41 * dpr + 1.0 * multiKillsPerRound + 1.0 * clutchKillsPerRound;

            // Console.WriteLine($"\nImpact Calculation:");
            // Console.WriteLine($"KPR: {kpr:F3} (Kills: {player.Kills})");
            // Console.WriteLine($"DPR: {dpr:F3} (Deaths: {player.Deaths})");
            // Console.WriteLine($"APR: {apr:F3} (Assists: {player.Assists})");
            // Console.WriteLine($"MultiKills/Round: {multiKillsPerRound:F3} (Total MultiKills: {multiKills})");
            // Console.WriteLine($"ClutchKills/Round: {clutchKillsPerRound:F3}");
            // Console.WriteLine($"Final Impact: {impact:F3}");

            return impact;
        }

        private double EstimateKAST(Player player, double avgRounds)
        {
            // Use the stats directly as they're already per-match averages
            double killRoundRate = Math.Min(player.Kills / avgRounds, 1.0);
            double assistRoundRate = Math.Min(player.Assists / avgRounds, 1.0);
            double survivalRate = Math.Max(1 - (player.Deaths / avgRounds), 0);
            double tradeEstimate = Math.Min((player.Assists + player.FlashSuccesses) / avgRounds, 0.3);

            double kastEstimate = (killRoundRate + assistRoundRate + survivalRate + tradeEstimate) / 4;
            double kastPercentage = Math.Min(Math.Max(kastEstimate * 100, 50), 85);

            // Console.WriteLine($"\nKAST Estimation:");
            // Console.WriteLine($"Kill Round Rate: {killRoundRate:F3}");
            // Console.WriteLine($"Assist Round Rate: {assistRoundRate:F3}");
            // Console.WriteLine($"Survival Rate: {survivalRate:F3}");
            // Console.WriteLine($"Trade Estimate: {tradeEstimate:F3}");
            // Console.WriteLine($"KAST Estimate: {kastEstimate:F3}");
            // Console.WriteLine($"Final KAST %: {kastPercentage:F1}%");

            return kastPercentage;
        }

        public double CalculateRating(Player player, double totalRounds, int matchCount)
        {

            double avgRounds =0;

            if (totalRounds == 0) return 0;
            // Console.WriteLine($"\nRating Calculation for {totalRounds} rounds:");
            // Console.WriteLine($"Base Stats - K: {player.Kills}, D: {player.Deaths}, A: {player.Assists}, ADR: {player.ADR:F1}");

            if (matchCount > 1){
                avgRounds = totalRounds / matchCount;
            }else{
                avgRounds = totalRounds;
            }
            

            // Convert per-match stats to per-round stats
            double kpr = player.Kills / avgRounds;
            double dpr = player.Deaths / avgRounds;
            double impact = CalculateImpact(player, avgRounds);
            double kast = EstimateKAST(player, avgRounds);

            // Break down each component's contribution
            double kastComponent = 0.0073 * kast;
            double kprComponent = 0.3591 * kpr;
            double dprComponent = -0.5329 * dpr;
            double impactComponent = 0.2372 * impact;
            double adrComponent = 0.0032 * player.ADR;
            double baseComponent = 0.1587;

            // Console.WriteLine($"\nRating Components:");
            // Console.WriteLine($"KAST Component: {kastComponent:F3} (KAST: {kast:F1}%)");
            // Console.WriteLine($"KPR Component: {kprComponent:F3} (KPR: {kpr:F3})");
            // Console.WriteLine($"DPR Component: {dprComponent:F3} (DPR: {dpr:F3})");
            // Console.WriteLine($"Impact Component: {impactComponent:F3} (Impact: {impact:F3})");
            // Console.WriteLine($"ADR Component: {adrComponent:F3} (ADR: {player.ADR:F1})");
            // Console.WriteLine($"Base Component: {baseComponent:F3}");
            // Console.WriteLine($"Total Rounds: {totalRounds} (Avg: {avgRounds:F1})");

            double rawRating = kastComponent + kprComponent + dprComponent + impactComponent + adrComponent + baseComponent;
            double scaledRating = rawRating * 1.1;

            // Console.WriteLine($"\nFinal Calculation:");
            // Console.WriteLine($"Raw Rating: {rawRating:F3}");
            // Console.WriteLine($"Scaled Rating (1.3x): {scaledRating:F3}");
            // Console.WriteLine($"Final Rating (Rounded): {Math.Round(scaledRating, 2):F2}\n");

            return Math.Round(scaledRating, 2);
        }
    }
}
