namespace focus_ai;

/// <summary>
/// Time-domain and non-linear heart-rate-variability measures calculated from
/// beat-to-beat NN/RR intervals. Instantaneous BPM samples are deliberately not
/// converted to RR intervals: they are not a valid substitute for beat timing.
/// </summary>
public sealed record HrvMetricsResult
{
    public int RrSampleCount { get; init; }
    public bool HasTimeDomainMetrics { get; init; }
    public double? SdnnMs { get; init; }
    public double? RmssdMs { get; init; }
    public bool HasDfaAlpha1 { get; init; }
    public double? DfaAlpha1 { get; init; }
    public bool HasSampleEntropy { get; init; }
    public double? SampleEntropy { get; init; }
    public bool IsAvailable => HasTimeDomainMetrics || HasDfaAlpha1 || HasSampleEntropy;
}

public static class HrvMetrics
{
    public const double MinimumRrMs = 300;
    public const double MaximumRrMs = 2000;
    public const int MinimumIntervalsForTimeDomain = 2;
    public const int MinimumIntervalsForComplexity = 20;

    /// <summary>
    /// Returns only finite physiological NN intervals. Artifact correction is a
    /// clinical/protocol decision and therefore is intentionally not inferred here.
    /// </summary>
    public static bool IsValidRrInterval(double rrMilliseconds) =>
        double.IsFinite(rrMilliseconds) &&
        rrMilliseconds >= MinimumRrMs && rrMilliseconds <= MaximumRrMs;

    public static HrvMetricsResult Analyze(IEnumerable<double>? rrIntervalsMs)
    {
        double[] rr = (rrIntervalsMs ?? Array.Empty<double>())
            .Where(IsValidRrInterval)
            .ToArray();

        if (rr.Length < MinimumIntervalsForTimeDomain)
            return new HrvMetricsResult { RrSampleCount = rr.Length };

        double mean = rr.Average();
        double sampleVariance = rr.Sum(value => (value - mean) * (value - mean)) / (rr.Length - 1);
        double sdnn = Math.Sqrt(Math.Max(0, sampleVariance));
        double rmssd = Math.Sqrt(rr
            .Zip(rr.Skip(1), (left, right) => (right - left) * (right - left))
            .Average());

        double? dfaAlpha1 = CalculateDfaAlpha1(rr);
        double? sampleEntropy = CalculateSampleEntropy(rr, sdnn);

        return new HrvMetricsResult
        {
            RrSampleCount = rr.Length,
            HasTimeDomainMetrics = double.IsFinite(sdnn) && double.IsFinite(rmssd),
            SdnnMs = FiniteOrNull(sdnn),
            RmssdMs = FiniteOrNull(rmssd),
            HasDfaAlpha1 = dfaAlpha1.HasValue,
            DfaAlpha1 = dfaAlpha1,
            HasSampleEntropy = sampleEntropy.HasValue,
            SampleEntropy = sampleEntropy
        };
    }

    /// <summary>
    /// DFA alpha1 over the short-term 4–16 beat range. Each scale is detrended
    /// with a linear fit over non-overlapping windows before log-log regression.
    /// </summary>
    private static double? CalculateDfaAlpha1(IReadOnlyList<double> rr)
    {
        if (rr.Count < MinimumIntervalsForComplexity)
            return null;

        double mean = rr.Average();
        var profile = new double[rr.Count];
        double cumulative = 0;
        for (int index = 0; index < rr.Count; index++)
        {
            cumulative += rr[index] - mean;
            profile[index] = cumulative;
        }

        int[] scales = { 4, 5, 6, 7, 8, 10, 12, 16 };
        var logScales = new List<double>();
        var logFluctuations = new List<double>();
        foreach (int scale in scales)
        {
            int windowCount = profile.Length / scale;
            if (windowCount < 2)
                continue;

            double squaredResiduals = 0;
            int residualCount = 0;
            for (int window = 0; window < windowCount; window++)
            {
                int offset = window * scale;
                (double intercept, double slope) = FitLine(profile, offset, scale);
                for (int localIndex = 0; localIndex < scale; localIndex++)
                {
                    double residual = profile[offset + localIndex] - (intercept + slope * localIndex);
                    squaredResiduals += residual * residual;
                    residualCount++;
                }
            }

            double fluctuation = residualCount > 0 ? Math.Sqrt(squaredResiduals / residualCount) : 0;
            if (double.IsFinite(fluctuation) && fluctuation > 1e-12)
            {
                logScales.Add(Math.Log(scale));
                logFluctuations.Add(Math.Log(fluctuation));
            }
        }

        return logScales.Count >= 4
            ? FiniteOrNull(LinearSlope(logScales, logFluctuations))
            : null;
    }

    /// <summary>
    /// Sample entropy (m=2, r=0.2 × SDNN) using the Chebyshev template distance.
    /// Self-matches are excluded as required by SampEn.
    /// </summary>
    private static double? CalculateSampleEntropy(IReadOnlyList<double> rr, double sdnn)
    {
        if (rr.Count < MinimumIntervalsForComplexity || !double.IsFinite(sdnn) || sdnn <= 1e-12)
            return null;

        const int embeddingDimension = 2;
        double tolerance = 0.2 * sdnn;
        long matchesAtM = CountTemplateMatches(rr, embeddingDimension, tolerance);
        long matchesAtMPlusOne = CountTemplateMatches(rr, embeddingDimension + 1, tolerance);
        if (matchesAtM == 0 || matchesAtMPlusOne == 0)
            return null;

        return FiniteOrNull(-Math.Log(matchesAtMPlusOne / (double)matchesAtM));
    }

    private static long CountTemplateMatches(IReadOnlyList<double> values, int length, double tolerance)
    {
        int templateCount = values.Count - length + 1;
        if (templateCount < 2)
            return 0;

        long matches = 0;
        for (int first = 0; first < templateCount - 1; first++)
        {
            for (int second = first + 1; second < templateCount; second++)
            {
                bool isMatch = true;
                for (int component = 0; component < length; component++)
                {
                    if (Math.Abs(values[first + component] - values[second + component]) > tolerance)
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                    matches++;
            }
        }

        return matches;
    }

    private static (double intercept, double slope) FitLine(IReadOnlyList<double> values, int offset, int length)
    {
        double xMean = (length - 1) / 2.0;
        double yMean = 0;
        for (int index = 0; index < length; index++)
            yMean += values[offset + index];
        yMean /= length;

        double numerator = 0;
        double denominator = 0;
        for (int index = 0; index < length; index++)
        {
            double centeredX = index - xMean;
            numerator += centeredX * (values[offset + index] - yMean);
            denominator += centeredX * centeredX;
        }

        double slope = denominator > 1e-12 ? numerator / denominator : 0;
        return (yMean - slope * xMean, slope);
    }

    private static double LinearSlope(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double xMean = x.Average();
        double yMean = y.Average();
        double numerator = 0;
        double denominator = 0;
        for (int index = 0; index < x.Count; index++)
        {
            double centeredX = x[index] - xMean;
            numerator += centeredX * (y[index] - yMean);
            denominator += centeredX * centeredX;
        }

        return denominator > 1e-12 ? numerator / denominator : double.NaN;
    }

    private static double? FiniteOrNull(double value) => double.IsFinite(value) ? value : null;
}
