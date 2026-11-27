using System.Globalization;
using System.IO;

namespace focus_ai;

/// <summary>
/// Quality-aware summary of the eye-tracker CSV. A gaze sample is valid only
/// when the ray intersects the calibrated monitor. Target engagement is
/// measured only during the moving-orange-dot phase, where an explicit AOI
/// exists. The image phase is not mislabelled as on-task fixation.
/// </summary>
public sealed record GazeSessionSummary
{
    public int TotalSamples { get; init; }
    public int ValidSamples { get; init; }
    public int TargetSamples { get; init; }
    public int TrackedTargetSamples { get; init; }
    public int OnTargetSamples { get; init; }

    public double? ValidFraction => TotalSamples > 0 ? ValidSamples / (double)TotalSamples : null;
    public double? OnTargetFraction => TrackedTargetSamples > 0
        ? OnTargetSamples / (double)TrackedTargetSamples
        : null;
    public double TargetTrackingCoverage => TargetSamples > 0
        ? TrackedTargetSamples / (double)TargetSamples
        : 0;

    public static GazeSessionSummary Empty { get; } = new();

    public static GazeSessionSummary ReadCsv(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Empty;

        int total = 0;
        int valid = 0;
        int target = 0;
        int trackedTarget = 0;
        int onTarget = 0;

        try
        {
            foreach (string line in File.ReadLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] fields = line.Split(',');
                if (fields.Length < 9 ||
                    !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int phase) ||
                    !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int gazeValid) ||
                    !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetAvailable))
                    continue;

                total++;
                if (gazeValid == 1)
                    valid++;

                if (phase != 2 || targetAvailable != 1)
                    continue;

                target++;
                if (gazeValid != 1 ||
                    !TryDouble(fields[3], out double gx) ||
                    !TryDouble(fields[4], out double gy) ||
                    !TryDouble(fields[6], out double tx) ||
                    !TryDouble(fields[7], out double ty) ||
                    !TryDouble(fields[8], out double radius) ||
                    radius <= 0)
                    continue;

                trackedTarget++;
                double dx = gx - tx;
                double dy = gy - ty;
                if (dx * dx + dy * dy <= radius * radius)
                    onTarget++;
            }
        }
        catch (IOException)
        {
            return Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return Empty;
        }

        return new GazeSessionSummary
        {
            TotalSamples = total,
            ValidSamples = valid,
            TargetSamples = target,
            TrackedTargetSamples = trackedTarget,
            OnTargetSamples = onTarget
        };
    }

    private static bool TryDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
        double.IsFinite(result);
}
