using System.Globalization;

namespace focus_ai;

public static class TestScoreCalculator
{
    public const int CurrentVersion = 2;

    public static double ComputeScore(string? mapData, double reactionTimeSeconds, double goNoGoAccuracy, double cptAccuracy, string? distanceData)
    {
        double mapScore = 0;
        if (!string.IsNullOrWhiteSpace(mapData))
        {
            string[] points = mapData.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int inRange = 0;
            foreach (string point in points)
            {
                string[] coordinates = point.Split(',', StringSplitOptions.TrimEntries);
                if (coordinates.Length == 2 &&
                    double.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                    double.IsFinite(x) && double.IsFinite(y) && x >= 0 && x <= 100 && y >= 0 && y <= 100)
                    inRange++;
            }

            mapScore = points.Length > 0 ? (double)inRange / points.Length * 45.0 : 0;
        }

        double rtScore = double.IsFinite(reactionTimeSeconds) && reactionTimeSeconds > 0
            ? Math.Min(25.0 / reactionTimeSeconds, 25.0)
            : 0;

        double validGoNoGo = double.IsFinite(goNoGoAccuracy) ? Math.Clamp(goNoGoAccuracy, 0, 100) : 0;
        double validCpt = double.IsFinite(cptAccuracy) ? Math.Clamp(cptAccuracy, 0, 100) : 0;
        double goNoGoScore = validGoNoGo / 100.0 * 20.0;
        double cptScore = validCpt / 100.0 * 10.0;

        string[] distanceSamples = string.IsNullOrWhiteSpace(distanceData)
            ? Array.Empty<string>()
            : distanceData.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int zeroCount = distanceSamples.Count(value =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            double.IsFinite(parsed) && parsed == 0);
        int validCount = distanceSamples.Count(value =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            double.IsFinite(parsed) && (parsed == 0 || parsed == 1));
        double zeroRate = validCount > 0 ? zeroCount / (double)validCount : 0;
        double penalty = 5.0 * zeroRate;

        return Math.Round(Math.Clamp(mapScore + rtScore + goNoGoScore + cptScore - penalty, 0, 100), 2);
    }
}
