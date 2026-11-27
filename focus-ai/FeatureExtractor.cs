using System.Globalization;
using System.Text.Json;
using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;


public static class FeatureExtractor
{
    public static SessionFeatures FromJsonElement(JsonElement el)
    {
        bool hasScore = TryGetDouble(el, out double score, "scor", "score");
        bool hasScoreVersion = TryGetDouble(el, out double scoreVersion, "scoreVersion");
        bool hasTr2 = TryGetDouble(el, out double tr2, "tr2");
        bool hasGoNoGo = TryGetDouble(el, out double goNoGo, "precizieGonogo", "precizie_gonogo");
        string mapData = GetString(el, "map");
        bool hasCptAccuracy = TryGetNestedDouble(el, "cpt", "accuracy", out double cptValue);
        double? cptAccuracy = hasCptAccuracy ? cptValue : null;

        string durationText = GetString(el, "duration");
        string dateTimeText = GetString(el, "dateTime", "datetime", "timestamp");

        double[] hr = ParseCsvDoubles(GetString(el, "hr"))
            .Where(v => v >= 30 && v <= 220)
            .ToArray();
        var (hrMean, hrStd, hrMin, hrMax) = MeanStdMinMax(hr);

        
        double[] spo2 = ParseCsvDoubles(GetString(el, "spo2"))
            .Where(v => v > 0 && v <= 100)
            .ToArray();
        double spo2Mean = spo2.Length > 0 ? spo2.Average() : 0;
        double spo2Min = spo2.Length > 0 ? spo2.Min() : 0;
        double spo2Drops = spo2.Count(v => v < 95);

        double[] dist = ParseCsvDoubles(GetString(el, "dist"))
            .Where(v => v == 0 || v == 1)
            .ToArray();
        double distRatio = dist.Length > 0
            ? dist.Count(v => v > 0.5) / (double)dist.Length
            : 0;

        // Raw MyoWare ADC samples are not a validated ECG waveform. Only
        // admit data into the legacy ECG feature slots when the producer has
        // explicitly marked the signal as clinically validated ECG.
        bool isValidatedEcg = TryGetBool(el, out bool validatedEcg, "analog_signal_is_clinically_validated_ecg") && validatedEcg;
        var (ecgDrMean, ecgStMean, ecgRange, ecgCount) = isValidatedEcg
            ? EcgStats(GetString(el, "analog_signal", "ecg"))
            : (0d, 0d, 0d, 0);
        var (mapXVar, mapYVar, mapPath, mapCount) = MapStats(GetString(el, "map"));
        CptDistributionFeatures cpt = ExtractCptDistribution(el);
        HrvMetricsResult hrv = ExtractHrvMetrics(el);

        AttentionSessionFeatures attention = ExtractAttention(
            el,
            mapData,
            hasTr2 ? tr2 : null,
            hasGoNoGo ? goNoGo : null,
            cptAccuracy,
            cpt.HitRate,
            cpt.FalseAlarmRate,
            cpt.TauMs,
            cpt.ReactionTimeCv,
            cpt.ReactionTimeSampleCount,
            hrv);

        // A valid legacy score remains historical data. Recomputing v1/v2 rows
        // under the v5 state-space model would silently change the longitudinal
        // record. Only records with no usable score receive a best-effort v5
        // estimate from their raw measurements.
        bool scoreWithinRange = hasScore && score >= 0 && score <= 100;
        if (!scoreWithinRange && attention.IsAvailable)
        {
            score = attention.Estimate * 100;
            hasScore = true;
            scoreWithinRange = true;
        }

        return new SessionFeatures
        {
            Score = hasScore ? score : 0,
            HasScoreData = scoreWithinRange,
            ScoreFormulaVersion = hasScoreVersion && scoreVersion >= 0 && scoreVersion <= int.MaxValue
                ? (int)Math.Round(scoreVersion)
                : 0,
            Tr2 = hasTr2 ? tr2 : 0,
            HasTr2Data = hasTr2,
            PrecizieGonogo = hasGoNoGo ? goNoGo : 0,
            HasPrecizieGonogoData = hasGoNoGo,

            CptMuMs = cpt.MuMs ?? 0,
            CptSigmaMs = cpt.SigmaMs ?? 0,
            CptTauMs = cpt.TauMs ?? 0,
            CptReactionTimeCv = cpt.ReactionTimeCv ?? 0,
            CptReactionTimeSampleCount = cpt.ReactionTimeSampleCount,
            HasCptExGaussianData = cpt.IsAvailable,

            HrMean = hrMean,
            HrStd = hrStd,
            HrMin = hrMin,
            HrMax = hrMax,
            HrRange = hr.Length > 0 ? hrMax - hrMin : 0,
            HrSampleCount = hr.Length,
            HasHrData = hr.Length > 0,

            Spo2Mean = spo2Mean,
            Spo2Min = spo2Min,
            Spo2DropCount = spo2Drops,
            Spo2SampleCount = spo2.Length,
            HasSpo2Data = spo2.Length > 0,

            DistRatio = distRatio,
            DistSampleCount = dist.Length,
            HasDistData = dist.Length > 0,

            EcgDrMean = ecgDrMean,
            EcgStMean = ecgStMean,
            EcgRange = ecgRange,
            EcgSampleCount = ecgCount,
            HasEcgData = ecgCount > 0,

            HrvSdnnMs = hrv.SdnnMs ?? 0,
            HrvRmssdMs = hrv.RmssdMs ?? 0,
            HrvDfaAlpha1 = hrv.DfaAlpha1 ?? 0,
            HrvSampleEntropy = hrv.SampleEntropy ?? 0,
            RrSampleCount = hrv.RrSampleCount,
            HasHrvData = hrv.IsAvailable,

            MapXVar = mapXVar,
            MapYVar = mapYVar,
            MapTotalPath = mapPath,
            MapSampleCount = mapCount,
            HasMapData = mapCount > 0,

            AttentionEstimate = attention.Estimate,
            AttentionPosteriorStandardDeviation = attention.PosteriorStandardDeviation,
            AttentionConfidenceLow = attention.ConfidenceLow,
            AttentionConfidenceHigh = attention.ConfidenceHigh,
            AttentionObservationCount = attention.ObservationCount,
            HasAttentionData = attention.IsAvailable,

            DurationMinutes = ParseDuration(durationText),
            DateTime = ParseDateTime(dateTimeText)
        };
    }

    private static CptDistributionFeatures ExtractCptDistribution(JsonElement root)
    {
        if (!root.TryGetProperty("cpt", out JsonElement cpt) || cpt.ValueKind != JsonValueKind.Object)
            return CptDistributionFeatures.Unavailable;

        double? mu = TryGetDouble(cpt, out double muValue, "mu_ms") && muValue > 0 ? muValue : null;
        double? sigma = TryGetDouble(cpt, out double sigmaValue, "sigma_ms") && sigmaValue > 0 ? sigmaValue : null;
        double? tau = TryGetDouble(cpt, out double tauValue, "tau_ms") && tauValue > 0 ? tauValue : null;
        double? cv = TryGetDouble(cpt, out double cvValue, "reaction_time_cv") && cvValue > 0 ? cvValue : null;
        double? hitRate = TryGetDouble(cpt, out double hitRateValue, "hit_rate") && hitRateValue is >= 0 and <= 1
            ? hitRateValue
            : null;
        double? falseAlarmRate = TryGetDouble(cpt, out double falseAlarmValue, "false_alarm_rate") && falseAlarmValue is >= 0 and <= 1
            ? falseAlarmValue
            : null;
        int storedSampleCount = TryGetDouble(cpt, out double storedCountValue, "reaction_time_sample_count") &&
                                storedCountValue is >= 0 and <= int.MaxValue
            ? (int)Math.Round(storedCountValue)
            : 0;

        double[] hitReactionTimes = ExtractHitReactionTimes(cpt);
        if ((!tau.HasValue || !mu.HasValue || !sigma.HasValue) && hitReactionTimes.Length >= 6)
        {
            ExGaussianFitResult fitted = ExGaussianFitter.Fit(hitReactionTimes);
            if (fitted.IsAvailable)
            {
                mu ??= fitted.MuMs;
                sigma ??= fitted.SigmaMs;
                tau ??= fitted.TauMs;
                storedSampleCount = Math.Max(storedSampleCount, fitted.SampleCount);
            }
        }

        if (!cv.HasValue && hitReactionTimes.Length > 1)
        {
            double mean = hitReactionTimes.Average();
            if (mean > 0)
            {
                double standardDeviation = Math.Sqrt(hitReactionTimes.Sum(value => (value - mean) * (value - mean)) /
                                                     (hitReactionTimes.Length - 1));
                if (double.IsFinite(standardDeviation))
                    cv = standardDeviation / mean;
            }
        }

        int sampleCount = Math.Max(storedSampleCount, hitReactionTimes.Length);
        bool hasReliabilityFlag = TryGetBool(cpt, out bool reportedReliable, "ex_gaussian_reliable");
        bool reliabilityAccepted = hasReliabilityFlag ? reportedReliable : sampleCount >= 8;
        if (!reliabilityAccepted)
        {
            // Keep the numeric fields out of ML when the fit failed convergence
            // or did not meet the minimum response-count threshold. The raw
            // values remain in Firebase for exploratory inspection.
            mu = null;
            sigma = null;
            tau = null;
        }

        return new CptDistributionFeatures(
            tau.HasValue,
            mu,
            sigma,
            tau,
            cv,
            sampleCount,
            hitRate,
            falseAlarmRate);
    }

    private static double[] ExtractHitReactionTimes(JsonElement cpt)
    {
        if (!cpt.TryGetProperty("trials", out JsonElement trials) || trials.ValueKind != JsonValueKind.Array)
            return Array.Empty<double>();

        var values = new List<double>();
        foreach (JsonElement trial in trials.EnumerateArray())
        {
            if (trial.ValueKind != JsonValueKind.Object ||
                !TryGetString(trial, out string responseType, "response_type") ||
                !responseType.Equals("HIT", StringComparison.OrdinalIgnoreCase) ||
                !TryGetDouble(trial, out double reactionTime, "reaction_time_ms") ||
                !double.IsFinite(reactionTime) || reactionTime <= 0)
            {
                continue;
            }

            values.Add(reactionTime);
        }

        return values.ToArray();
    }

    private static HrvMetricsResult ExtractHrvMetrics(JsonElement root)
    {
        // The numeric series alone cannot identify whether intervals came from
        // ECG R-peaks or PPG pulse peaks. Never mix unlabelled/legacy sources in
        // the physiological feature columns.
        string source = GetString(root, "rr_source");
        string measurementType = GetString(root, "rr_measurement_type");
        bool isSupportedPpgPrv = source.Equals("MAX30102_PPG", StringComparison.OrdinalIgnoreCase) &&
                                 measurementType.Equals("PRV", StringComparison.OrdinalIgnoreCase);
        bool isSupportedEcgRr = source.Contains("ECG", StringComparison.OrdinalIgnoreCase) &&
                                measurementType.Equals("RR", StringComparison.OrdinalIgnoreCase);
        if (!isSupportedPpgPrv && !isSupportedEcgRr)
            return new HrvMetricsResult();

        double[] rrIntervals = ParseCsvDoubles(GetString(root, "rr_ms", "rrMs", "rr"))
            .Where(HrvMetrics.IsValidRrInterval)
            .ToArray();
        if (rrIntervals.Length > 0)
            return HrvMetrics.Analyze(rrIntervals);

        if (!root.TryGetProperty("hrv", out JsonElement hrv) || hrv.ValueKind != JsonValueKind.Object)
            return new HrvMetricsResult();

        double? sdnn = TryGetDouble(hrv, out double sdnnValue, "sdnn_ms") && sdnnValue >= 0 ? sdnnValue : null;
        double? rmssd = TryGetDouble(hrv, out double rmssdValue, "rmssd_ms") && rmssdValue >= 0 ? rmssdValue : null;
        double? dfa = TryGetDouble(hrv, out double dfaValue, "dfa_alpha1") && dfaValue > 0 ? dfaValue : null;
        double? entropy = TryGetDouble(hrv, out double entropyValue, "sample_entropy") && entropyValue >= 0 ? entropyValue : null;
        int count = TryGetDouble(hrv, out double countValue, "rr_sample_count") && countValue is >= 0 and <= int.MaxValue
            ? (int)Math.Round(countValue)
            : 0;
        bool hasTimeDomain = TryGetBool(hrv, out bool storedTimeDomain, "has_time_domain_metrics")
            ? storedTimeDomain && (sdnn.HasValue || rmssd.HasValue)
            : sdnn.HasValue || rmssd.HasValue;
        bool hasDfa = TryGetBool(hrv, out bool storedDfa, "has_dfa_alpha1") ? storedDfa && dfa.HasValue : dfa.HasValue;
        bool hasEntropy = TryGetBool(hrv, out bool storedEntropy, "has_sample_entropy")
            ? storedEntropy && entropy.HasValue
            : entropy.HasValue;

        return new HrvMetricsResult
        {
            RrSampleCount = count,
            HasTimeDomainMetrics = hasTimeDomain,
            SdnnMs = hasTimeDomain ? sdnn : null,
            RmssdMs = hasTimeDomain ? rmssd : null,
            HasDfaAlpha1 = hasDfa,
            DfaAlpha1 = hasDfa ? dfa : null,
            HasSampleEntropy = hasEntropy,
            SampleEntropy = hasEntropy ? entropy : null
        };
    }

    private static AttentionSessionFeatures ExtractAttention(
        JsonElement root,
        string mapData,
        double? tr2,
        double? goNoGo,
        double? cptAccuracy,
        double? hitRate,
        double? falseAlarmRate,
        double? tauMs,
        double? reactionTimeCv,
        int reactionTimeSampleCount,
        HrvMetricsResult hrv)
    {
        if (root.TryGetProperty("attention", out JsonElement attention) && attention.ValueKind == JsonValueKind.Object &&
            TryGetDouble(attention, out double storedEstimate, "estimate") && storedEstimate is >= 0 and <= 1)
        {
            double standardDeviation = TryGetDouble(attention, out double storedSd, "posterior_standard_deviation") && storedSd >= 0
                ? storedSd
                : 0;
            double confidenceLow = TryGetDouble(attention, out double storedLow, "confidence_low") && storedLow is >= 0 and <= 1
                ? storedLow
                : Math.Clamp(storedEstimate - 1.96 * standardDeviation, 0, 1);
            double confidenceHigh = TryGetDouble(attention, out double storedHigh, "confidence_high") && storedHigh is >= 0 and <= 1
                ? storedHigh
                : Math.Clamp(storedEstimate + 1.96 * standardDeviation, 0, 1);
            int observationCount = TryGetDouble(attention, out double storedCount, "observation_count") &&
                                   storedCount is >= 0 and <= int.MaxValue
                ? (int)Math.Round(storedCount)
                : 0;
            return new AttentionSessionFeatures(true, storedEstimate, standardDeviation, confidenceLow, confidenceHigh, observationCount);
        }

        AttentionScoreResult reconstructed = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput
        {
            MapData = mapData,
            BuzzerReactionTimeSeconds = tr2,
            GoNoGoAccuracyPercent = goNoGo,
            CptAccuracyPercent = cptAccuracy,
            CptHitRate = hitRate,
            CptFalseAlarmRate = falseAlarmRate,
            CptTauMs = tauMs,
            CptReactionTimeCv = reactionTimeCv,
            CptReactionTimeSampleCount = reactionTimeSampleCount,
            Hrv = hrv
        });

        AttentionEstimate posterior = reconstructed.Posterior;
        return posterior.ObservationCount > 0
            ? new AttentionSessionFeatures(
                true,
                posterior.Estimate,
                posterior.PosteriorStandardDeviation,
                posterior.ConfidenceLow,
                posterior.ConfidenceHigh,
                posterior.ObservationCount)
            : AttentionSessionFeatures.Unavailable;
    }

    private sealed record CptDistributionFeatures(
        bool IsAvailable,
        double? MuMs,
        double? SigmaMs,
        double? TauMs,
        double? ReactionTimeCv,
        int ReactionTimeSampleCount,
        double? HitRate,
        double? FalseAlarmRate)
    {
        public static CptDistributionFeatures Unavailable { get; } = new(false, null, null, null, null, 0, null, null);
    }

    private sealed record AttentionSessionFeatures(
        bool IsAvailable,
        double Estimate,
        double PosteriorStandardDeviation,
        double ConfidenceLow,
        double ConfidenceHigh,
        int ObservationCount)
    {
        public static AttentionSessionFeatures Unavailable { get; } = new(false, 0, 0, 0, 0, 0);
    }

    private static string GetString(JsonElement el, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement p))
                continue;

            switch (p.ValueKind)
            {
                case JsonValueKind.String:
                    return p.GetString() ?? "";
                case JsonValueKind.Number:
                    return p.GetRawText();
                case JsonValueKind.Array:
                    var values = new List<string>();
                    foreach (JsonElement item in p.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number)
                            values.Add(item.GetRawText());
                        else if (item.ValueKind == JsonValueKind.String &&
                                 !string.IsNullOrWhiteSpace(item.GetString()))
                            values.Add(item.GetString()!);
                    }
                    return string.Join(",", values);
            }
        }

        return "";
    }

    private static bool TryGetNestedDouble(JsonElement el, string objectName, string propertyName, out double value)
    {
        value = 0;
        return el.TryGetProperty(objectName, out JsonElement nested) &&
               nested.ValueKind == JsonValueKind.Object &&
               TryGetDouble(nested, out value, propertyName);
    }

    private static bool TryGetString(JsonElement el, out string value, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement property))
                continue;

            if (property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? "";
                return true;
            }
        }

        value = "";
        return false;
    }

    private static bool TryGetBool(JsonElement el, out bool value, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement property))
                continue;

            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out value))
            {
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetDouble(JsonElement el, out double value, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement p))
                continue;

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out value) && double.IsFinite(value))
                return true;

            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                double.IsFinite(value))
                return true;
        }

        value = 0;
        return false;
    }

    private static double[] ParseCsvDoubles(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<double>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
                ? (double?)v
                : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
    }

    private static (double mean, double std, double min, double max) MeanStdMinMax(double[] values)
    {
        if (values.Length == 0)
            return (0, 0, 0, 0);

        double mean = values.Average();
        double variance = values.Select(v => (v - mean) * (v - mean)).Average();
        return (mean, Math.Sqrt(variance), values.Min(), values.Max());
    }

    private static double ParseDuration(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) &&
            double.IsFinite(minutes) && minutes >= 0)
            return minutes;

        string[] parts = s.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sec) &&
            m >= 0 && sec is >= 0 and < 60)
            return m + sec / 60.0;

        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mm) &&
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ss) &&
            h >= 0 && mm is >= 0 and < 60 && ss is >= 0 and < 60)
            return h * 60.0 + mm + ss / 60.0;

        return 0;
    }

    private static DateTime ParseDateTime(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return DateTime.MinValue;

        string[] formats =
        {
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd HH:mm:ss",
            "O"
        };

        if (DateTime.TryParseExact(
                s,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out DateTime exact))
            return exact;

        if (DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out DateTimeOffset offset))
            return offset.LocalDateTime;

        return DateTime.MinValue;
    }

    private static (double drMean, double stMean, double range, int count) EcgStats(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (0, 0, 0, 0);

        var drValues = new List<double>();
        var stValues = new List<double>();
        var allValues = new List<double>();
        int validPairs = 0;

        foreach (string pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pts = pair.Split(',', StringSplitOptions.TrimEntries);
            if (pts.Length < 2)
                continue;

            bool hasDr = double.TryParse(pts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double dr) && double.IsFinite(dr) && dr >= 0;
            bool hasSt = double.TryParse(pts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double st) && double.IsFinite(st) && st >= 0;

            if (hasDr)
            {
                drValues.Add(dr);
                allValues.Add(dr);
            }
            if (hasSt)
            {
                stValues.Add(st);
                allValues.Add(st);
            }
            if (hasDr && hasSt)
                validPairs++;
        }

        if (allValues.Count == 0)
            return (0, 0, 0, 0);

        return (
            drValues.Count > 0 ? drValues.Average() : 0,
            stValues.Count > 0 ? stValues.Average() : 0,
            allValues.Max() - allValues.Min(),
            drValues.Count);
    }

    private static (double xVar, double yVar, double totalPath, int count) MapStats(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (0, 0, 0, 0);

        var xs = new List<double>();
        var ys = new List<double>();

        foreach (string pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pts = pair.Split(',', StringSplitOptions.TrimEntries);
            if (pts.Length < 2)
                continue;

            bool okX = double.TryParse(pts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.IsFinite(x) && x >= 0 && x <= 100;
            bool okY = double.TryParse(pts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) && double.IsFinite(y) && y >= 0 && y <= 100;
            
            if (okX && okY)
            {
                xs.Add(x);
                ys.Add(y);
            }
        }

        if (xs.Count == 0)
            return (0, 0, 0, 0);

        double xMean = xs.Average();
        double yMean = ys.Average();
        double xVar = xs.Select(v => (v - xMean) * (v - xMean)).Average();
        double yVar = ys.Select(v => (v - yMean) * (v - yMean)).Average();

        double path = 0;
        for (int i = 1; i < xs.Count; i++)
        {
            double dx = xs[i] - xs[i - 1];
            double dy = ys[i] - ys[i - 1];
            path += Math.Sqrt(dx * dx + dy * dy);
        }

        return (xVar, yVar, path, xs.Count);
    }
}
