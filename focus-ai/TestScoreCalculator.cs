using System.Globalization;

namespace focus_ai;

/// <summary>
/// Inputs to the session-level latent-attention estimator. All observations are
/// optional because a session may legitimately run without one or more sensors.
/// </summary>
public sealed record AttentionEstimationInput
{
    public string? MapData { get; init; }
    public double? BuzzerReactionTimeSeconds { get; init; }
    public double? GoNoGoAccuracyPercent { get; init; }
    public double? CptAccuracyPercent { get; init; }
    public double? CptHitRate { get; init; }
    public double? CptFalseAlarmRate { get; init; }
    public double? CptTauMs { get; init; }
    public double? CptReactionTimeCv { get; init; }
    public int CptReactionTimeSampleCount { get; init; }
    public HrvMetricsResult? Hrv { get; init; }
    public double? PupilEngagement { get; init; }
    public int PupilSampleCount { get; init; }
}

/// <summary>Explainable subscores and the posterior attention estimate.</summary>
public sealed record AttentionScoreResult
{
    public AttentionEstimate Posterior { get; init; } = new AttentionKalmanEstimator().Estimate(null);
    public double? BehavioralSubscore { get; init; }
    public double? GazeOnScreenFraction { get; init; }
    public int GazeSampleCount { get; init; }
    public double? HrvAdaptivitySubscore { get; init; }
    public double? PupilEngagementSubscore { get; init; }
    public double? TauSubscore { get; init; }

    public double Score => Posterior.Score;
}

/// <summary>
/// Converts heterogeneous session signals into noisy observations and fuses
/// them with <see cref="AttentionKalmanEstimator"/>. The published score is the
/// posterior mean (0–100), not a hand-added list of point weights.
/// </summary>
public static class TestScoreCalculator
{
    public const int CurrentVersion = 3;

    /// <summary>
    /// Compatibility overload for historical records. distanceData is retained
    /// in the signature so old callers compile, but it is no longer used as an
    /// arbitrary penalty in the scientific estimator.
    /// </summary>
    public static double ComputeScore(
        string? mapData,
        double reactionTimeSeconds,
        double goNoGoAccuracy,
        double cptAccuracy,
        string? distanceData)
    {
        _ = distanceData;
        return EstimateAttention(new AttentionEstimationInput
        {
            MapData = mapData,
            BuzzerReactionTimeSeconds = IsPositiveFinite(reactionTimeSeconds) ? reactionTimeSeconds : null,
            GoNoGoAccuracyPercent = IsPercent(goNoGoAccuracy) ? goNoGoAccuracy : null,
            CptAccuracyPercent = IsPercent(cptAccuracy) ? cptAccuracy : null
        }).Score;
    }

    public static AttentionScoreResult EstimateAttention(AttentionEstimationInput? input)
    {
        input ??= new AttentionEstimationInput();
        var observations = new List<AttentionObservation>();

        (List<AttentionObservation> behavioralObservations, double? tauSubscore) =
            BuildBehavioralObservations(input);
        observations.AddRange(behavioralObservations);
        AttentionEstimate? behavioralPosterior = behavioralObservations.Count > 0
            ? new AttentionKalmanEstimator().Estimate(behavioralObservations)
            : null;

        GazeSummary gaze = AnalyzeGaze(input.MapData);
        if (gaze.IsAvailable)
        {
            // The current tracker gives on-screen validity, not an AOI-labelled
            // target fixation measure; its conservative R prevents over-weighting.
            double variance = Math.Clamp(0.08 + 0.20 / Math.Sqrt(gaze.SampleCount), 0.09, 0.22);
            observations.Add(new AttentionObservation(
                "gaze-on-screen", gaze.OnScreenFraction, variance,
                $"{gaze.ValidSampleCount}/{gaze.SampleCount} valid gaze samples"));
        }

        (double? hrvScore, double hrvVariance, string? hrvDetail) = BuildHrvObservation(input.Hrv);
        if (hrvScore.HasValue)
            observations.Add(new AttentionObservation("hrv-adaptivity", hrvScore.Value, hrvVariance, hrvDetail));

        double? pupilSubscore = null;
        if (input.PupilEngagement is double pupil && IsUnitInterval(pupil))
        {
            pupilSubscore = pupil;
            double sampleQuality = Math.Clamp(input.PupilSampleCount / 120.0, 0, 1);
            double variance = 0.10 + 0.14 * (1 - sampleQuality);
            observations.Add(new AttentionObservation(
                "pupil-engagement", pupil, variance,
                input.PupilSampleCount > 0 ? $"{input.PupilSampleCount} pupil samples" : "pupil quality unavailable"));
        }

        AttentionEstimate posterior = new AttentionKalmanEstimator().Estimate(observations);
        return new AttentionScoreResult
        {
            Posterior = posterior,
            BehavioralSubscore = behavioralPosterior?.Estimate,
            GazeOnScreenFraction = gaze.IsAvailable ? gaze.OnScreenFraction : null,
            GazeSampleCount = gaze.SampleCount,
            HrvAdaptivitySubscore = hrvScore,
            PupilEngagementSubscore = pupilSubscore,
            TauSubscore = tauSubscore
        };
    }

    public static GazeSummary AnalyzeGaze(string? mapData)
    {
        if (string.IsNullOrWhiteSpace(mapData))
            return GazeSummary.Unavailable;

        int total = 0;
        int valid = 0;
        foreach (string point in mapData.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] coordinates = point.Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 2)
                continue;

            total++;
            if (double.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                double.IsFinite(x) && double.IsFinite(y) && x >= 0 && x <= 100 && y >= 0 && y <= 100)
            {
                valid++;
            }
        }

        return total >= 5
            ? new GazeSummary(true, valid / (double)total, valid, total)
            : GazeSummary.Unavailable with { SampleCount = total, ValidSampleCount = valid };
    }

    /// <summary>
    /// Every behavioural measurement is an explicit observation with its own
    /// noise model. We intentionally do not add point weights: the Kalman gain
    /// controls influence from declared measurement precision and availability.
    /// CPT component variances are conservative because hit rate, accuracy and
    /// false alarms are correlated measurements of the same task.
    /// </summary>
    private static (List<AttentionObservation> Observations, double? TauSubscore) BuildBehavioralObservations(
        AttentionEstimationInput input)
    {
        var observations = new List<AttentionObservation>();
        if (input.CptAccuracyPercent is double cptAccuracy && IsPercent(cptAccuracy))
            observations.Add(new AttentionObservation("cpt-accuracy", cptAccuracy / 100, 0.11, "CPT accuracy"));
        if (input.GoNoGoAccuracyPercent is double goNoGoAccuracy && IsPercent(goNoGoAccuracy))
            observations.Add(new AttentionObservation("go-no-go-accuracy", goNoGoAccuracy / 100, 0.13, "Go/No-Go accuracy"));
        if (input.CptHitRate is double hitRate && IsUnitInterval(hitRate))
            observations.Add(new AttentionObservation("cpt-hit-rate", hitRate, 0.18, "CPT hit rate"));
        if (input.CptFalseAlarmRate is double falseAlarmRate && IsUnitInterval(falseAlarmRate))
            observations.Add(new AttentionObservation("cpt-inhibitory-control", 1 - falseAlarmRate, 0.18, "CPT false-alarm complement"));

        double? tauSubscore = null;
        if (input.CptTauMs is double tau && IsPositiveFinite(tau))
        {
            tauSubscore = DescendingLogistic(tau, midpoint: 300, scale: 90);
            double sampleQuality = Math.Clamp(input.CptReactionTimeSampleCount / 20.0, 0, 1);
            observations.Add(new AttentionObservation(
                "cpt-ex-gaussian-tau",
                tauSubscore.Value,
                0.14 + 0.20 * (1 - sampleQuality),
                $"τ={tau:F0} ms from {input.CptReactionTimeSampleCount} correct target responses"));
        }
        if (input.CptReactionTimeCv is double cv && IsPositiveFinite(cv))
            observations.Add(new AttentionObservation(
                "cpt-rt-variability",
                DescendingLogistic(cv, midpoint: 0.28, scale: 0.08),
                0.20,
                $"RT coefficient of variation={cv:F2}"));
        if (input.BuzzerReactionTimeSeconds is double rt && IsPositiveFinite(rt))
            observations.Add(new AttentionObservation(
                "auditory-reaction-time",
                DescendingLogistic(rt * 1000, midpoint: 430, scale: 115),
                0.25,
                $"auditory RT={rt * 1000:F0} ms"));

        return (observations, tauSubscore);
    }

    private static (double? Value, double Variance, string? Detail) BuildHrvObservation(HrvMetricsResult? hrv)
    {
        if (hrv is null || !hrv.IsAvailable)
            return (null, 0, null);

        var components = new List<(double Value, double Weight, string Name)>();
        if (hrv.SdnnMs is double sdnn && IsPositiveFinite(sdnn))
            components.Add((BellScore(sdnn, centre: 55, halfWidth: 55), 0.22, "SDNN"));
        if (hrv.RmssdMs is double rmssd && IsPositiveFinite(rmssd))
            components.Add((BellScore(rmssd, centre: 42, halfWidth: 42), 0.22, "RMSSD"));
        if (hrv.DfaAlpha1 is double dfa && IsPositiveFinite(dfa))
            components.Add((BellScore(dfa, centre: 1.0, halfWidth: 0.55), 0.30, "DFA α1"));
        if (hrv.SampleEntropy is double entropy && IsPositiveFinite(entropy))
            components.Add((BellScore(entropy, centre: 1.25, halfWidth: 1.00), 0.26, "sample entropy"));

        if (components.Count == 0)
            return (null, 0, null);

        double totalWeight = components.Sum(component => component.Weight);
        double value = components.Sum(component => component.Value * component.Weight) / totalWeight;
        double metricCompleteness = Math.Clamp(totalWeight, 0, 1);
        double intervalQuality = Math.Clamp(hrv.RrSampleCount / 60.0, 0, 1);
        double variance = Math.Clamp(0.13 + 0.12 * (1 - metricCompleteness) + 0.12 * (1 - intervalQuality), 0.13, 0.37);
        return (value, variance, $"{hrv.RrSampleCount} RR intervals; {string.Join(", ", components.Select(component => component.Name))}");
    }

    private static double BellScore(double value, double centre, double halfWidth)
    {
        if (!double.IsFinite(value) || halfWidth <= 0)
            return 0;

        return Math.Clamp(1 - Math.Abs(value - centre) / halfWidth, 0, 1);
    }

    private static double DescendingLogistic(double value, double midpoint, double scale)
    {
        double exponent = Math.Clamp((value - midpoint) / scale, -40, 40);
        return 1 / (1 + Math.Exp(exponent));
    }

    private static bool IsPercent(double value) => double.IsFinite(value) && value >= 0 && value <= 100;
    private static bool IsUnitInterval(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}

/// <summary>Gaze validity summary used as a conservative on-screen proxy.</summary>
public sealed record GazeSummary(bool IsAvailable, double OnScreenFraction, int ValidSampleCount, int SampleCount)
{
    public static GazeSummary Unavailable { get; } = new(false, 0, 0, 0);
}
