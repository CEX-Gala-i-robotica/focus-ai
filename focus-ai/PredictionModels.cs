using System;

namespace focus_ai.Prediction.Models
{
    public enum TrendDirection
    {
        Improving,
        Stable,
        Declining
    }

    public record SessionFeatures
    {
        public double Score { get; init; }
        public double Tr2 { get; init; }
        public double PrecizieGonogo { get; init; }

        public double HrMean { get; init; }
        public double HrStd { get; init; }
        public double HrMin { get; init; }
        public double HrMax { get; init; }
        public double HrRange { get; init; }

        public double Spo2Mean { get; init; }
        public double Spo2Min { get; init; }
        public double Spo2DropCount { get; init; }

        public double DistRatio { get; init; }

        public double EcgDrMean { get; init; }
        public double EcgStMean { get; init; }
        public double EcgRange { get; init; }

        public double MapXVar { get; init; }
        public double MapYVar { get; init; }
        public double MapTotalPath { get; init; }

        public double DurationMinutes { get; init; }
        public DateTime DateTime { get; init; }
    }

    public record PredictionResult
    {
        public double PredictedScore { get; init; }
        public double ConfidenceLow { get; init; }
        public double ConfidenceHigh { get; init; }

        public double LinearRegrScore { get; init; }
        public double HoltScore { get; init; }
        public double KnnScore { get; init; }

        public TrendDirection Trend { get; init; }
        public string TrendLabel { get; init; } = "";
        public double TrendPerSession { get; init; }

        public bool AlertSpO2Low { get; init; }
        public bool AlertHrAnormal { get; init; }
        public bool AlertHrVariabil { get; init; }
        public string AlertMessage { get; init; } = "";

        public int SessionsUsed { get; init; }
        public double ModelMAE { get; init; }
        public string ConfidenceLabel { get; init; } = "";
    }
}
