using System.IO;
using System.Text.Json;
using Microsoft.ML;
using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;






public sealed class PredictionEngine
{
    private readonly double _holtAlpha;
    private readonly double _holtBeta;
    private readonly int _knnK;
    private readonly int _windowSize;
    private readonly MLContext _ml;
    private ITransformer? _trainedModel;
    private PredictionModelMetadata? _metadata;

    public PredictionEngine(
        double holtAlpha = 0.45,
        double holtBeta = 0.30,
        int knnK = 3,
        int windowSize = 12)
    {
        _holtAlpha = Math.Clamp(holtAlpha, 0.01, 0.99);
        _holtBeta = Math.Clamp(holtBeta, 0.01, 0.99);
        _knnK = Math.Max(1, knnK);
        _windowSize = Math.Max(2, windowSize);
        _ml = new MLContext(seed: 42);
    }

    
    public PredictionEngine(
        string modelPath,
        double holtAlpha = 0.45,
        double holtBeta = 0.30,
        int knnK = 3,
        int windowSize = 12)
        : this(holtAlpha, holtBeta, knnK, windowSize)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Calea modelului nu poate fi goală.", nameof(modelPath));

        string fullPath = Path.GetFullPath(modelPath);
        string metadataPath = fullPath + ".meta.json";
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException(
                $"Fișierul de metadate nu există: {metadataPath}. Rulează mai întâi TrainAndSave sau construiește PredictionEngine fără cale.",
                metadataPath);

        _metadata = JsonSerializer.Deserialize<PredictionModelMetadata>(File.ReadAllText(metadataPath))
                    ?? throw new InvalidDataException("Fișierul de metadate al modelului este invalid.");

        if (_metadata.ArtifactVersion != 2 || _metadata.FeatureSchemaVersion != "next-session-v2")
            throw new InvalidDataException(
                $"Schema modelului ({_metadata.FeatureSchemaVersion}) nu este compatibilă cu această versiune de FeatureBuilder.");

        if (NextSessionModelTrainer.IsMlModel(_metadata.SelectedModel))
        {
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Modelul ML.NET nu există: {fullPath}", fullPath);
            _trainedModel = _ml.Model.Load(fullPath, out _);
        }
    }

    public PredictionResult Predict(IReadOnlyList<SessionFeatures> history, int horizon = 1)
    {
        if (history is null)
            throw new ArgumentNullException(nameof(history));
        if (horizon < 1)
            throw new ArgumentOutOfRangeException(nameof(horizon), "Orizontul trebuie să fie cel puțin 1.");

        List<SessionFeatures> ordered = NextSessionFeatureBuilder.OrderAndValidate(history);
        if (ordered.Count < 2)
            throw new ArgumentException("Sunt necesare cel puțin două sesiuni valide pentru predicție.", nameof(history));

        double[] scores = ordered.Select(s => s.Score).ToArray();
        double linearScore = BaselineForecasts.LinearRegressionForecast(scores, horizon, _windowSize);
        double holtScore = BaselineForecasts.HoltForecast(scores, horizon, _windowSize, _holtAlpha, _holtBeta);
        double naiveScore = BaselineForecasts.Predict("NaiveLastScore", ordered, horizon, _windowSize, _knnK, _holtAlpha, _holtBeta);
        double movingAverageScore = BaselineForecasts.Predict("MovingAverage3", ordered, horizon, _windowSize, _knnK, _holtAlpha, _holtBeta);
        double knnScore = BaselineForecasts.Predict("KNN", ordered, 1, _windowSize, _knnK, _holtAlpha, _holtBeta);

        string modelName;
        double predictedScore;
        bool usedMl = false;

        if (horizon > 1)
        {
            
            
            modelName = "Holt (multi-step fallback)";
            predictedScore = holtScore;
        }
        else if (_metadata is not null)
        {
            modelName = _metadata.SelectedModel;
            if (NextSessionModelTrainer.IsMlModel(modelName) && _trainedModel is not null)
            {
                NextSessionRow row = NextSessionFeatureBuilder.BuildForNextSession(ordered);
                predictedScore = NextSessionModelTrainer.PredictWithModel(_ml, _trainedModel, row);
                usedMl = true;
            }
            else
            {
                predictedScore = BaselineForecasts.Predict(
                    modelName, ordered, horizon, _windowSize, _knnK, _holtAlpha, _holtBeta);
            }
        }
        else
        {
            modelName = SelectBestBaseline(ordered);
            predictedScore = BaselineForecasts.Predict(
                modelName, ordered, horizon, _windowSize, _knnK, _holtAlpha, _holtBeta);
        }

        predictedScore = Math.Clamp(double.IsFinite(predictedScore) ? predictedScore : holtScore, 0, 100);
        double trendPerSession = BaselineForecasts.LinearRegressionFit(
            scores.TakeLast(Math.Min(scores.Length, _windowSize)).ToArray()).slope;

        TrendDirection trend;
        string trendLabel;
        if (trendPerSession > 3)
        {
            trend = TrendDirection.Improving;
            trendLabel = "Improving";
        }
        else if (trendPerSession < -3)
        {
            trend = TrendDirection.Declining;
            trendLabel = "Declining";
        }
        else
        {
            trend = TrendDirection.Stable;
            trendLabel = "Stable";
        }

        SessionFeatures last = ordered[^1];
        bool alertSpo2 = last.HasSpo2Data &&
                         (last.Spo2Mean < 95 ||
                          (last.Spo2SampleCount > 0 && last.Spo2DropCount / last.Spo2SampleCount >= 0.05));
        bool alertHrAbnormal = last.HasHrData && last.HrMean > 0 && (last.HrMean < 45 || last.HrMean > 110);
        bool alertHrVar = last.HasHrData && last.HrStd > 25;

        double modelMae = GetModelMae(modelName, ordered);
        (double low, double high, string intervalLabel) = GetPredictionInterval(
            modelName, predictedScore, horizon, ordered);

        string intervalStatusLabel = intervalLabel.Contains("rolling-origin", StringComparison.OrdinalIgnoreCase)
            ? "Rolling-origin empirical interval"
            : intervalLabel.Contains("90%", StringComparison.Ordinal)
                ? "Empirical 90% interval"
                : "Uncalibrated interval";

        return new PredictionResult
        {
            PredictedScore = Math.Round(predictedScore, 1),
            PredictionIntervalLow = Math.Round(low, 1),
            PredictionIntervalHigh = Math.Round(high, 1),
            LinearRegrScore = Math.Round(linearScore, 1),
            HoltScore = Math.Round(holtScore, 1),
            KnnScore = Math.Round(knnScore, 1),
            BaselineNaiveScore = Math.Round(naiveScore, 1),
            MovingAverageScore = Math.Round(movingAverageScore, 1),
            Trend = trend,
            TrendLabel = trendLabel,
            TrendPerSession = Math.Round(trendPerSession, 2),
            AlertSpO2Low = alertSpo2,
            AlertHrAnormal = alertHrAbnormal,
            AlertHrVariabil = alertHrVar,
            AlertMessage = BuildAlertMessage(alertSpo2, alertHrAbnormal, alertHrVar, last),
            SessionsUsed = ordered.Count,
            ModelMAE = double.IsFinite(modelMae) ? Math.Round(modelMae, 2) : null,
            IntervalStatusLabel = intervalStatusLabel,
            TestIntervalCoverage90 = _metadata?.TestIntervalCoverage90,
            IntervalLabel = intervalLabel,
            ModelName = modelName,
            PredictionHorizon = horizon,
            UsedMachineLearningModel = usedMl,
            DataQualityLabel = BuildDataQualityLabel(last)
        };
    }

    private string SelectBestBaseline(IReadOnlyList<SessionFeatures> history)
    {
        List<NextSessionTrainingSample> backtestSamples = NextSessionFeatureBuilder.BuildTrainingSamples(history);
        if (backtestSamples.Count < 2)
            return "Holt";

        List<NextSessionTrainingSample> evaluation = backtestSamples.TakeLast(Math.Min(_windowSize, backtestSamples.Count)).ToList();
        return BaselineForecasts.CandidateNames
            .Select(name => new
            {
                Name = name,
                Mae = evaluation.Average(sample => Math.Abs(
                    sample.TargetScore - BaselineForecasts.Predict(
                        name, sample.HistoryBeforeTarget, 1, _windowSize, _knnK, _holtAlpha, _holtBeta)))
            })
            .OrderBy(x => x.Mae)
            .First().Name;
    }

    private double GetModelMae(string modelName, IReadOnlyList<SessionFeatures> history)
    {
        if (_metadata is not null && string.Equals(_metadata.SelectedModel, modelName, StringComparison.Ordinal))
            return _metadata.TestMae;

        if (modelName.StartsWith("Holt (multi-step", StringComparison.Ordinal))
            modelName = "Holt";

        List<NextSessionTrainingSample> samples = NextSessionFeatureBuilder.BuildTrainingSamples(history);
        if (samples.Count == 0)
            return double.NaN;

        List<NextSessionTrainingSample> recent = samples.TakeLast(Math.Min(samples.Count, _windowSize)).ToList();
        return recent.Average(s => Math.Abs(
            s.TargetScore - BaselineForecasts.Predict(modelName, s.HistoryBeforeTarget, 1, _windowSize, _knnK, _holtAlpha, _holtBeta)));
    }

    private (double low, double high, string label) GetPredictionInterval(
        string modelName,
        double prediction,
        int horizon,
        IReadOnlyList<SessionFeatures> history)
    {
        if (horizon != 1)
            return (0, 100, "Ne-calibrat pentru orizont multiplu");

        double? quantile = null;
        string label = "";

        if (_metadata is not null &&
            string.Equals(_metadata.SelectedModel, modelName, StringComparison.Ordinal) &&
            _metadata.CalibrationQuantile90.HasValue)
        {
            quantile = _metadata.CalibrationQuantile90.Value;
            label = $"Interval empiric nominal 90% ({_metadata.CalibrationExamples} exemple de calibrare)";
        }
        else if (_metadata is null || !NextSessionModelTrainer.IsMlModel(modelName))
        {
            
            List<NextSessionTrainingSample> samples = NextSessionFeatureBuilder.BuildTrainingSamples(history);
            double[] errors = samples
                .TakeLast(Math.Min(samples.Count, Math.Max(_windowSize, 5)))
                .Select(s => Math.Abs(s.TargetScore - BaselineForecasts.Predict(
                    modelName, s.HistoryBeforeTarget, 1, _windowSize, _knnK, _holtAlpha, _holtBeta)))
                .Where(double.IsFinite)
                .OrderBy(v => v)
                .ToArray();

            int oneBasedIndex = (int)Math.Ceiling((errors.Length + 1) * 0.90);
            if (errors.Length >= 9 && oneBasedIndex <= errors.Length)
            {
                quantile = errors[oneBasedIndex - 1];
                label = $"Interval empiric rolling-origin nominal 90% ({errors.Length} erori istorice)";
            }
        }

        if (!quantile.HasValue || !double.IsFinite(quantile.Value) || quantile.Value < 0)
            return (0, 100, "Insuficiente date pentru calibrarea intervalului");

        return (
            Math.Max(0, prediction - quantile.Value),
            Math.Min(100, prediction + quantile.Value),
            label);
    }

    private static string BuildDataQualityLabel(SessionFeatures last)
    {
        int sensorCount = (last.HasHrData ? 1 : 0) +
                          (last.HasSpo2Data ? 1 : 0) +
                          (last.HasEcgData ? 1 : 0) +
                          (last.HasMapData ? 1 : 0);
        return $"Senzori disponibili: {sensorCount}/4; HR={last.HrSampleCount}, SpO2={last.Spo2SampleCount}, ECG={last.EcgSampleCount}, MAP={last.MapSampleCount} eșantioane valide.";
    }

    private static string BuildAlertMessage(bool spo2, bool heartRate, bool heartRateVariability, SessionFeatures last)
    {
        if (!spo2 && !heartRate && !heartRateVariability)
            return "";

        var parts = new List<string>();
        if (spo2)
        {
            double dropRate = last.Spo2SampleCount > 0 ? last.Spo2DropCount / last.Spo2SampleCount : 0;
            parts.Add($"Low SpO2 (mean {last.Spo2Mean:F1}%, fraction below 95%: {dropRate:P1})");
        }
        if (heartRate)
            parts.Add($"Abnormal heart rate (mean {last.HrMean:F0} bpm)");
        if (heartRateVariability)
            parts.Add($"High dispersion in heart-rate samples (standard deviation {last.HrStd:F1} bpm)");
        return string.Join(" | ", parts);
    }
}
