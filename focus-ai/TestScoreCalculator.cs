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
    public double? GazeOnTargetFraction { get; init; }
    public int GazeTargetSampleCount { get; init; }
    public double? GazeTargetTrackingCoverage { get; init; }
    public double? GazeValidFraction { get; init; }
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
    public double? GazeOnTargetFraction { get; init; }
    public int GazeTargetSampleCount { get; init; }
    public double? GazeValidFraction { get; init; }
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
    public const int CurrentVersion = 5;

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

        // The legacy map string contains only successful coordinates. It cannot
        // represent tracking dropouts and must not be used as an on-screen or
        // attention score. A gaze observation is admitted only from the
        // timestamped CSV with an explicit target AOI (phase 2).
        GazeSummary gaze = AnalyzeGaze(input.MapData);
        double? gazeOnTarget = input.GazeOnTargetFraction;
        if (gazeOnTarget is double targetFraction && IsUnitInterval(targetFraction) &&
            input.GazeTargetSampleCount >= 10)
        {
            double coverage = input.GazeTargetTrackingCoverage is double c && IsUnitInterval(c) ? c : 0;
            double sampleQuality = Math.Clamp(input.GazeTargetSampleCount / 120.0, 0, 1);
            double variance = Math.Clamp(0.10 + 0.16 * (1 - sampleQuality) + 0.22 * (1 - coverage), 0.10, 0.48);
            observations.Add(new AttentionObservation(
                "gaze-on-target",
                targetFraction,
                variance,
                $"{input.GazeTargetSampleCount} tracked AOI samples; tracking coverage={coverage:P0}"));
        }

        // MAX30102 values are PPG-derived PRV, not ECG HRV. Keep the descriptive
        // composite for reporting only until a calibrated physiological model
        // demonstrates that it is an independent attention observation.
        var prvSummary = BuildHrvObservation(input.Hrv);
        double? hrvScore = prvSummary.Value;

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
            GazeOnScreenFraction = input.GazeValidFraction is double validFraction && IsUnitInterval(validFraction) ? validFraction : null,
            GazeSampleCount = input.GazeTargetSampleCount > 0 ? input.GazeTargetSampleCount : gaze.SampleCount,
            GazeOnTargetFraction = gazeOnTarget,
            GazeTargetSampleCount = input.GazeTargetSampleCount,
            GazeValidFraction = input.GazeValidFraction,
            HrvAdaptivitySubscore = hrvScore,
            PupilEngagementSubscore = pupilSubscore,
            TauSubscore = tauSubscore
        };
    }

    public static GazeSummary AnalyzeGaze(string? mapData)
    {
        if (string.IsNullOrWhiteSpace(mapData))
            return GazeSummary.Unavailable;

        // Historical map values contain successful gaze coordinates only. They
        // do not include failed tracking frames or target positions, therefore
        // they are suitable for visualization but not psychometric scoring.
        int count = mapData.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return GazeSummary.Unavailable with { SampleCount = count };
    }

    /// <summary>
    /// Each statistically distinct task family contributes one observation.
    /// CPT accuracy, hit rate and false alarms are correlated, as are tau and
    /// RT coefficient of variation, so they are combined before entering the
    /// filter instead of being counted as independent evidence.
    /// </summary>
    private static (List<AttentionObservation> Observations, double? TauSubscore) BuildBehavioralObservations(
        AttentionEstimationInput input)
    {
        var observations = new List<AttentionObservation>();

        // Balanced accuracy uses both target detection and inhibitory control.
        // If both rates are available, do not add overall accuracy separately.
        double? cptPerformance = null;
        string cptPerformanceDetail = "CPT balanced performance";
        if (input.CptHitRate is double hitRate && IsUnitInterval(hitRate) &&
            input.CptFalseAlarmRate is double falseAlarmRate && IsUnitInterval(falseAlarmRate))
        {
            cptPerformance = (hitRate + (1 - falseAlarmRate)) / 2.0;
            cptPerformanceDetail = $"CPT balanced performance (hit={hitRate:F2}, false alarm={falseAlarmRate:F2})";
        }
        else if (input.CptAccuracyPercent is double cptAccuracy && IsPercent(cptAccuracy))
        {
            cptPerformance = cptAccuracy / 100.0;
        }
        else if (input.CptHitRate is double onlyHit && IsUnitInterval(onlyHit))
        {
            cptPerformance = onlyHit;
            cptPerformanceDetail = "CPT hit rate (fallback; false-alarm rate unavailable)";
        }
        else if (input.CptFalseAlarmRate is double onlyFalseAlarm && IsUnitInterval(onlyFalseAlarm))
        {
            cptPerformance = 1 - onlyFalseAlarm;
            cptPerformanceDetail = "CPT false-alarm complement (fallback; hit rate unavailable)";
        }

        if (cptPerformance.HasValue)
            observations.Add(new AttentionObservation("cpt-balanced-performance", cptPerformance.Value, 0.16, cptPerformanceDetail));

        if (input.GoNoGoAccuracyPercent is double goNoGoAccuracy && IsPercent(goNoGoAccuracy))
            observations.Add(new AttentionObservation("go-no-go-accuracy", goNoGoAccuracy / 100, 0.13, "Go/No-Go accuracy"));

        double? tauSubscore = null;
        double? rtTailScore = null;
        var rtDetails = new List<string>();
        if (input.CptTauMs is double tau && IsPositiveFinite(tau))
        {
            tauSubscore = DescendingLogistic(tau, midpoint: 300, scale: 90);
            rtTailScore = tauSubscore.Value * 0.65;
            rtDetails.Add($"τ={tau:F0} ms");
        }
        if (input.CptReactionTimeCv is double cv && IsPositiveFinite(cv))
        {
            double cvSubscore = DescendingLogistic(cv, midpoint: 0.28, scale: 0.08);
            rtTailScore = (rtTailScore ?? 0) + cvSubscore * (tauSubscore.HasValue ? 0.35 : 1.0);
            rtDetails.Add($"RT-CV={cv:F2}");
        }
        if (rtTailScore.HasValue)
        {
            // Fewer correct target responses make tau itself less stable, so
            // its effective observation noise grows when the sample is small.
            double sampleQuality = Math.Clamp(input.CptReactionTimeSampleCount / 20.0, 0, 1);
            observations.Add(new AttentionObservation(
                "cpt-rt-tail-variability",
                Math.Clamp(rtTailScore.Value, 0, 1),
                0.16 + 0.20 * (1 - sampleQuality),
                $"CPT response-time tail/variability: {string.Join(", ", rtDetails)}; n={input.CptReactionTimeSampleCount}"));
        }

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
        return (value, variance, $"{hrv.RrSampleCount} PPG pulse-to-pulse intervals (PRV); {string.Join(", ", components.Select(component => component.Name))}");
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

/// <summary>Legacy map metadata; intentionally never treated as an attention observation.</summary>
public sealed record GazeSummary(bool IsAvailable, double OnScreenFraction, int ValidSampleCount, int SampleCount)
{
    public static GazeSummary Unavailable { get; } = new(false, 0, 0, 0);
}
