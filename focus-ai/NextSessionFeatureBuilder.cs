using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;

/// <summary>
/// Builds next-session examples without target leakage.
/// For target session i, predictors only see sessions [0..i-1].
/// </summary>
public static class NextSessionFeatureBuilder
{
    public static NextSessionRow BuildForNextSession(IReadOnlyList<SessionFeatures> history)
    {
        List<SessionFeatures> ordered = OrderAndValidate(history);
        if (ordered.Count < 2)
            throw new ArgumentException("Sunt necesare cel puțin două sesiuni valide pentru predicție.", nameof(history));

        return BuildFeatureRow(ordered, label: 0);
    }

    internal static List<NextSessionTrainingSample> BuildTrainingSamples(IReadOnlyList<SessionFeatures> history)
    {
        List<SessionFeatures> ordered = OrderAndValidate(history);
        var samples = new List<NextSessionTrainingSample>();

        // Start at target index 2: every training example has at least two complete prior sessions.
        for (int targetIndex = 2; targetIndex < ordered.Count; targetIndex++)
        {
            List<SessionFeatures> prefix = ordered.Take(targetIndex).ToList();
            SessionFeatures target = ordered[targetIndex];
            var row = BuildFeatureRow(prefix, target.Score);
            samples.Add(new NextSessionTrainingSample(row, prefix, target.Score));
        }

        return samples;
    }

    internal static List<SessionFeatures> OrderAndValidate(IReadOnlyList<SessionFeatures> history)
    {
        if (history is null)
            throw new ArgumentNullException(nameof(history));

        // Invalid/missing target scores cannot be used for lag features or supervised labels.
        var valid = history
            .Where(s => s is not null && s.HasScoreData && double.IsFinite(s.Score) && s.Score >= 0 && s.Score <= 100)
            .ToList();

        // If all timestamps exist, order explicitly. Otherwise preserve caller order; sorting a mixture
        // of valid and missing timestamps could silently reorder a user's history incorrectly.
        if (valid.Count > 1 && valid.All(s => s.DateTime != DateTime.MinValue))
            valid = valid.OrderBy(s => s.DateTime).ToList();

        return valid;
    }

    private static NextSessionRow BuildFeatureRow(IReadOnlyList<SessionFeatures> history, double label)
    {
        SessionFeatures last = history[^1];
        double[] scores = history.Select(s => s.Score).ToArray();
        double[] lastThree = scores.TakeLast(3).ToArray();
        double mean3 = lastThree.Average();
        double variance3 = lastThree.Select(v => (v - mean3) * (v - mean3)).Average();

        bool hasPrevious = history.Count >= 2;
        bool hasTwoPrevious = history.Count >= 3;
        double scoreLag1 = hasPrevious ? history[^2].Score : 0;
        double scoreLag2 = hasTwoPrevious ? history[^3].Score : 0;

        bool hasTimeGap = hasPrevious && last.DateTime != DateTime.MinValue && history[^2].DateTime != DateTime.MinValue;
        double daysSincePrevious = hasTimeGap
            ? Math.Clamp((last.DateTime - history[^2].DateTime).TotalDays, 0, 365)
            : 0;
        bool hasTimestamp = last.DateTime != DateTime.MinValue;
        double hourSin = 0, hourCos = 0, weekdaySin = 0, weekdayCos = 0;
        if (hasTimestamp)
        {
            double hourAngle = 2 * Math.PI * (last.DateTime.Hour + last.DateTime.Minute / 60.0) / 24.0;
            hourSin = Math.Sin(hourAngle);
            hourCos = Math.Cos(hourAngle);
            double dayAngle = 2 * Math.PI * (int)last.DateTime.DayOfWeek / 7.0;
            weekdaySin = Math.Sin(dayAngle);
            weekdayCos = Math.Cos(dayAngle);
        }

        return new NextSessionRow
        {
            Label = ToFloat(label),
            ScoreLast = ToFloat(last.Score),
            ScoreLag1 = ToFloat(scoreLag1),
            ScoreLag2 = ToFloat(scoreLag2),
            ScoreMean3 = ToFloat(mean3),
            ScoreStd3 = ToFloat(Math.Sqrt(variance3)),
            ScoreDelta1 = ToFloat(hasPrevious ? last.Score - history[^2].Score : 0),
            ScoreDelta2 = ToFloat(hasTwoPrevious ? last.Score - history[^3].Score : 0),
            ScoreTrend3 = ToFloat(LinearSlope(lastThree)),
            HistoryCount = ToFloat(Math.Min(history.Count, 100)),

            Tr2 = ToFloat(last.Tr2),
            HasTr2Data = last.HasTr2Data ? 1 : 0,
            PrecizieGonogo = ToFloat(last.PrecizieGonogo),
            HasPrecizieGonogoData = last.HasPrecizieGonogoData ? 1 : 0,

            HrMean = ToFloat(last.HrMean),
            HrStd = ToFloat(last.HrStd),
            HrRange = ToFloat(last.HrRange),
            HrHasData = last.HasHrData ? 1 : 0,
            HrSampleCount = ToFloat(last.HrSampleCount),

            Spo2Mean = ToFloat(last.Spo2Mean),
            Spo2Min = ToFloat(last.Spo2Min),
            Spo2DropCount = ToFloat(last.Spo2DropCount),
            Spo2HasData = last.HasSpo2Data ? 1 : 0,
            Spo2SampleCount = ToFloat(last.Spo2SampleCount),

            DistRatio = ToFloat(last.DistRatio),
            DistHasData = last.HasDistData ? 1 : 0,
            DistSampleCount = ToFloat(last.DistSampleCount),

            EcgDrMean = ToFloat(last.EcgDrMean),
            EcgStMean = ToFloat(last.EcgStMean),
            EcgRange = ToFloat(last.EcgRange),
            EcgHasData = last.HasEcgData ? 1 : 0,
            EcgSampleCount = ToFloat(last.EcgSampleCount),

            MapXVar = ToFloat(last.MapXVar),
            MapYVar = ToFloat(last.MapYVar),
            MapTotalPath = ToFloat(last.MapTotalPath),
            MapHasData = last.HasMapData ? 1 : 0,
            MapSampleCount = ToFloat(last.MapSampleCount),

            DurationMinutes = ToFloat(last.DurationMinutes),
            DaysSincePrevious = ToFloat(daysSincePrevious),
            HasTimeGapData = hasTimeGap ? 1 : 0,
            SessionHourSin = ToFloat(hourSin),
            SessionHourCos = ToFloat(hourCos),
            DayOfWeekSin = ToFloat(weekdaySin),
            DayOfWeekCos = ToFloat(weekdayCos),
            HasTimestampData = hasTimestamp ? 1 : 0
        };
    }

    private static double LinearSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 0;

        double xMean = (values.Count - 1) / 2.0;
        double yMean = values.Average();
        double numerator = 0, denominator = 0;
        for (int i = 0; i < values.Count; i++)
        {
            numerator += (i - xMean) * (values[i] - yMean);
            denominator += (i - xMean) * (i - xMean);
        }
        return denominator > 1e-12 ? numerator / denominator : 0;
    }

    internal static float ToFloat(double value)
    {
        if (!double.IsFinite(value))
            return 0;

        // Avoid infinities after casting unusually large finite doubles to Single.
        return (float)Math.Clamp(value, -1_000_000, 1_000_000);
    }
}
