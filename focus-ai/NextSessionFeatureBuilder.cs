using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;





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

        
        var valid = history
            .Where(s => s is not null && s.HasScoreData && double.IsFinite(s.Score) && s.Score >= 0 && s.Score <= 100)
            .Select(Sanitize)
            .ToList();

        
        
        if (valid.Count > 1 && valid.All(s => s.DateTime != DateTime.MinValue))
            valid = valid.OrderBy(s => s.DateTime).ToList();

        return valid;
    }


    private static SessionFeatures Sanitize(SessionFeatures session)
    {
        bool hasTr2 = session.HasTr2Data && double.IsFinite(session.Tr2) && session.Tr2 > 0 && session.Tr2 <= 600;
        bool hasGoNoGo = session.HasPrecizieGonogoData && double.IsFinite(session.PrecizieGonogo) && session.PrecizieGonogo >= 0 && session.PrecizieGonogo <= 100;
        bool hasHr = session.HasHrData && session.HrSampleCount > 0 &&
                     double.IsFinite(session.HrMean) && session.HrMean >= 25 && session.HrMean <= 250 &&
                     double.IsFinite(session.HrStd) && session.HrStd >= 0 &&
                     double.IsFinite(session.HrRange) && session.HrRange >= 0;
        bool hasSpo2 = session.HasSpo2Data && session.Spo2SampleCount > 0 &&
                       double.IsFinite(session.Spo2Mean) && session.Spo2Mean > 0 && session.Spo2Mean <= 100 &&
                       double.IsFinite(session.Spo2Min) && session.Spo2Min > 0 && session.Spo2Min <= 100 &&
                       double.IsFinite(session.Spo2DropCount) && session.Spo2DropCount >= 0;
        bool hasDist = session.HasDistData && session.DistSampleCount > 0 &&
                       double.IsFinite(session.DistRatio) && session.DistRatio >= 0 && session.DistRatio <= 1;
        bool hasEcg = session.HasEcgData && session.EcgSampleCount > 0 &&
                      double.IsFinite(session.EcgDrMean) && double.IsFinite(session.EcgStMean) &&
                      double.IsFinite(session.EcgRange) && session.EcgRange >= 0;
        bool hasMap = session.HasMapData && session.MapSampleCount > 0 &&
                      double.IsFinite(session.MapXVar) && session.MapXVar >= 0 &&
                      double.IsFinite(session.MapYVar) && session.MapYVar >= 0 &&
                      double.IsFinite(session.MapTotalPath) && session.MapTotalPath >= 0;
        bool hasDuration = double.IsFinite(session.DurationMinutes) && session.DurationMinutes >= 0 && session.DurationMinutes <= 1440;

        return session with
        {
            Tr2 = hasTr2 ? session.Tr2 : 0,
            HasTr2Data = hasTr2,
            PrecizieGonogo = hasGoNoGo ? session.PrecizieGonogo : 0,
            HasPrecizieGonogoData = hasGoNoGo,
            HrMean = hasHr ? session.HrMean : 0,
            HrStd = hasHr ? session.HrStd : 0,
            HrRange = hasHr ? session.HrRange : 0,
            HrMin = hasHr && double.IsFinite(session.HrMin) ? session.HrMin : 0,
            HrMax = hasHr && double.IsFinite(session.HrMax) ? session.HrMax : 0,
            HrSampleCount = hasHr ? session.HrSampleCount : 0,
            HasHrData = hasHr,
            Spo2Mean = hasSpo2 ? session.Spo2Mean : 0,
            Spo2Min = hasSpo2 ? session.Spo2Min : 0,
            Spo2DropCount = hasSpo2 ? session.Spo2DropCount : 0,
            Spo2SampleCount = hasSpo2 ? session.Spo2SampleCount : 0,
            HasSpo2Data = hasSpo2,
            DistRatio = hasDist ? session.DistRatio : 0,
            DistSampleCount = hasDist ? session.DistSampleCount : 0,
            HasDistData = hasDist,
            EcgDrMean = hasEcg ? session.EcgDrMean : 0,
            EcgStMean = hasEcg ? session.EcgStMean : 0,
            EcgRange = hasEcg ? session.EcgRange : 0,
            EcgSampleCount = hasEcg ? session.EcgSampleCount : 0,
            HasEcgData = hasEcg,
            MapXVar = hasMap ? session.MapXVar : 0,
            MapYVar = hasMap ? session.MapYVar : 0,
            MapTotalPath = hasMap ? session.MapTotalPath : 0,
            MapSampleCount = hasMap ? session.MapSampleCount : 0,
            HasMapData = hasMap,
            DurationMinutes = hasDuration ? session.DurationMinutes : 0
        };
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
            HrSamplesPerMinute = ToFloat(SamplesPerMinute(last.HrSampleCount, last.DurationMinutes)),

            Spo2Mean = ToFloat(last.Spo2Mean),
            Spo2Min = ToFloat(last.Spo2Min),
            Spo2DropRate = ToFloat(last.Spo2SampleCount > 0 ? last.Spo2DropCount / last.Spo2SampleCount : 0),
            Spo2HasData = last.HasSpo2Data ? 1 : 0,
            Spo2SamplesPerMinute = ToFloat(SamplesPerMinute(last.Spo2SampleCount, last.DurationMinutes)),

            DistRatio = ToFloat(last.DistRatio),
            DistHasData = last.HasDistData ? 1 : 0,
            DistReadingsPerMinute = ToFloat(SamplesPerMinute(last.DistSampleCount, last.DurationMinutes)),

            EcgDrMean = ToFloat(last.EcgDrMean),
            EcgStMean = ToFloat(last.EcgStMean),
            EcgRange = ToFloat(last.EcgRange),
            EcgHasData = last.HasEcgData ? 1 : 0,
            EcgPairsPerMinute = ToFloat(SamplesPerMinute(last.EcgSampleCount, last.DurationMinutes)),

            MapXVar = ToFloat(last.MapXVar),
            MapYVar = ToFloat(last.MapYVar),
            MapTotalPath = ToFloat(last.MapTotalPath),
            MapHasData = last.HasMapData ? 1 : 0,
            MapPointsPerMinute = ToFloat(SamplesPerMinute(last.MapSampleCount, last.DurationMinutes)),

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

    private static double SamplesPerMinute(int count, double durationMinutes) =>
        count > 0 && double.IsFinite(durationMinutes) && durationMinutes > 0
            ? count / durationMinutes
            : 0;

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

        
        return (float)Math.Clamp(value, -1_000_000, 1_000_000);
    }
}
