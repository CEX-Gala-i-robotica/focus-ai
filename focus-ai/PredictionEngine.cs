using System;
using System.Collections.Generic;
using System.Linq;
using focus_ai.Prediction.Models;

namespace focus_ai.Prediction
{
    public class PredictionEngine
    {
        private readonly double _holtAlpha;
        private readonly double _holtBeta;
        private readonly int _knnK;
        private readonly int _windowSize;

        public PredictionEngine(double holtAlpha = 0.45, double holtBeta = 0.30, int knnK = 3, int windowSize = 12)
        {
            _holtAlpha = holtAlpha;
            _holtBeta = holtBeta;
            _knnK = knnK;
            _windowSize = windowSize;
        }

        public PredictionResult Predict(IReadOnlyList<SessionFeatures> history, int horizon = 1)
        {
            if (history == null || history.Count < 2)
                throw new ArgumentException("At least 2 sessions are required.", nameof(history));

            var ordered = history.OrderBy(h => h.DateTime).ToList();
            var window = ordered.Count > _windowSize ? ordered.Skip(ordered.Count - _windowSize).ToList() : ordered;
            double[] scores = window.Select(s => s.Score).ToArray();
            int n = scores.Length;

            var (lrA, lrB) = LinearRegressionFit(scores);
            double linRegScore = Math.Clamp(lrA + lrB * (n - 1 + horizon), 0, 100);

            var (holtLevel, holtTrend) = HoltFit(scores);
            double holtScore = Math.Clamp(holtLevel + holtTrend * horizon, 0, 100);

            double knnScore = KnnPredict(ordered[^1], ordered.SkipLast(1).ToList());

            double wL, wH, wK;
            if (n <= 3) { wL = 0.15; wH = 0.65; wK = 0.20; }
            else if (n <= 6) { wL = 0.25; wH = 0.50; wK = 0.25; }
            else if (n <= 12) { wL = 0.35; wH = 0.40; wK = 0.25; }
            else { wL = 0.40; wH = 0.35; wK = 0.25; }

            double ensembleScore = Math.Clamp(wL * linRegScore + wH * holtScore + wK * knnScore, 0, 100);

            double[] residuals = LeaveOneOutResiduals(scores);
            double mae = residuals.Select(Math.Abs).DefaultIfEmpty(0).Average();
            double std = residuals.Length > 1 ? Math.Sqrt(residuals.Select(r => r * r).Average()) : mae;

            double trendPerSession = lrB;
            TrendDirection trend;
            string trendLabel;
            if (trendPerSession > 3) { trend = TrendDirection.Improving; trendLabel = "Improving"; }
            else if (trendPerSession < -3) { trend = TrendDirection.Declining; trendLabel = "Declining"; }
            else { trend = TrendDirection.Stable; trendLabel = "Stable"; }

            SessionFeatures last = ordered[^1];
            bool alertSpo2 = last.Spo2Mean > 0 && (last.Spo2Mean < 95 || last.Spo2DropCount > 3);
            bool alertHrAb = last.HrMean > 0 && (last.HrMean < 45 || last.HrMean > 110);
            bool alertHrVar = last.HrStd > 25;

            return new PredictionResult
            {
                PredictedScore = Math.Round(ensembleScore, 1),
                ConfidenceLow = Math.Max(0, Math.Round(ensembleScore - 1.28 * std, 1)),
                ConfidenceHigh = Math.Min(100, Math.Round(ensembleScore + 1.28 * std, 1)),
                LinearRegrScore = Math.Round(linRegScore, 1),
                HoltScore = Math.Round(holtScore, 1),
                KnnScore = Math.Round(knnScore, 1),
                Trend = trend,
                TrendLabel = trendLabel,
                TrendPerSession = Math.Round(trendPerSession, 2),
                AlertSpO2Low = alertSpo2,
                AlertHrAnormal = alertHrAb,
                AlertHrVariabil = alertHrVar,
                AlertMessage = BuildAlertMessage(alertSpo2, alertHrAb, alertHrVar, last),
                SessionsUsed = n,
                ModelMAE = Math.Round(mae, 2),
                ConfidenceLabel = mae < 5 ? "High" : mae < 12 ? "Medium" : "Low"
            };
        }

        private static (double a, double b) LinearRegressionFit(double[] y)
        {
            int n = y.Length;
            double[] t = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            double tMean = t.Average();
            double yMean = y.Average();
            double ssXY = 0;
            double ssXX = 0;
            for (int i = 0; i < n; i++)
            {
                ssXY += (t[i] - tMean) * (y[i] - yMean);
                ssXX += (t[i] - tMean) * (t[i] - tMean);
            }

            double b = ssXX > 1e-10 ? ssXY / ssXX : 0;
            return (yMean - b * tMean, b);
        }

        private (double level, double trend) HoltFit(double[] y)
        {
            if (y.Length == 1) return (y[0], 0);
            double level = y[0];
            double trend = y[1] - y[0];

            for (int i = 1; i < y.Length; i++)
            {
                double previousLevel = level;
                level = _holtAlpha * y[i] + (1 - _holtAlpha) * (level + trend);
                trend = _holtBeta * (level - previousLevel) + (1 - _holtBeta) * trend;
            }

            return (level, trend);
        }

        private double KnnPredict(SessionFeatures query, IReadOnlyList<SessionFeatures> candidates)
        {
            if (candidates.Count == 0) return query.Score;
            var all = candidates.Append(query).ToList();
            var norms = ComputeNormalizationBounds(all);
            int k = Math.Min(_knnK, candidates.Count);
            const double eps = 1e-9;

            var nearest = candidates
                .Select(c => new { Distance = EuclideanDistance(query, c, norms), c.Score })
                .OrderBy(x => x.Distance)
                .Take(k)
                .ToList();

            double totalWeight = nearest.Sum(x => 1.0 / (x.Distance + eps));
            return totalWeight < eps
                ? nearest.Average(x => x.Score)
                : nearest.Sum(x => x.Score * (1.0 / (x.Distance + eps))) / totalWeight;
        }

        private static double EuclideanDistance(SessionFeatures a, SessionFeatures b, FeatureNorms n)
        {
            static double Norm(double v, double lo, double hi) => hi - lo < 1e-9 ? 0 : (v - lo) / (hi - lo);
            double[] diff =
            {
                Norm(a.HrMean, n.HrMeanLo, n.HrMeanHi) - Norm(b.HrMean, n.HrMeanLo, n.HrMeanHi),
                Norm(a.HrStd, n.HrStdLo, n.HrStdHi) - Norm(b.HrStd, n.HrStdLo, n.HrStdHi),
                Norm(a.Spo2Mean, n.Spo2Lo, n.Spo2Hi) - Norm(b.Spo2Mean, n.Spo2Lo, n.Spo2Hi),
                Norm(a.Spo2DropCount, n.Spo2DLo, n.Spo2DHi) - Norm(b.Spo2DropCount, n.Spo2DLo, n.Spo2DHi),
                Norm(a.DistRatio, 0, 1) - Norm(b.DistRatio, 0, 1),
                Norm(a.HrRange, n.HrRangeLo, n.HrRangeHi) - Norm(b.HrRange, n.HrRangeLo, n.HrRangeHi),
                Norm(a.EcgRange, n.EcgRangeLo, n.EcgRangeHi) - Norm(b.EcgRange, n.EcgRangeLo, n.EcgRangeHi),
                Norm(a.DurationMinutes, n.DurLo, n.DurHi) - Norm(b.DurationMinutes, n.DurLo, n.DurHi)
            };

            return Math.Sqrt(diff.Sum(d => d * d));
        }

        private static FeatureNorms ComputeNormalizationBounds(IEnumerable<SessionFeatures> all)
        {
            var list = all.ToList();
            return new FeatureNorms(
                list.Min(s => s.HrMean), list.Max(s => s.HrMean),
                list.Min(s => s.HrStd), list.Max(s => s.HrStd),
                list.Min(s => s.HrRange), list.Max(s => s.HrRange),
                list.Min(s => s.Spo2Mean), list.Max(s => s.Spo2Mean),
                list.Min(s => s.Spo2DropCount), list.Max(s => s.Spo2DropCount),
                list.Min(s => s.EcgRange), list.Max(s => s.EcgRange),
                list.Min(s => s.DurationMinutes), list.Max(s => s.DurationMinutes));
        }

        private double[] LeaveOneOutResiduals(double[] scores)
        {
            if (scores.Length < 3) return new[] { 0.0 };
            var residuals = new List<double>();
            for (int leaveOut = 1; leaveOut < scores.Length; leaveOut++)
            {
                double[] train = scores.Take(leaveOut).ToArray();
                var (level, trend) = HoltFit(train);
                double pred = Math.Clamp(level + trend, 0, 100);
                residuals.Add(scores[leaveOut] - pred);
            }

            return residuals.ToArray();
        }

        private static string BuildAlertMessage(bool spo2, bool hrAb, bool hrVar, SessionFeatures last)
        {
            if (!spo2 && !hrAb && !hrVar) return "";
            var parts = new List<string>();
            if (spo2) parts.Add($"Low SpO2 (avg {last.Spo2Mean:F1}%, {(int)last.Spo2DropCount} values < 95%)");
            if (hrAb) parts.Add($"Abnormal heart rate (avg {last.HrMean:F0} bpm)");
            if (hrVar) parts.Add($"High heart-rate variability (std = {last.HrStd:F1} bpm)");
            return string.Join(" | ", parts);
        }

        private record FeatureNorms(
            double HrMeanLo, double HrMeanHi,
            double HrStdLo, double HrStdHi,
            double HrRangeLo, double HrRangeHi,
            double Spo2Lo, double Spo2Hi,
            double Spo2DLo, double Spo2DHi,
            double EcgRangeLo, double EcgRangeHi,
            double DurLo, double DurHi);
    }
}
