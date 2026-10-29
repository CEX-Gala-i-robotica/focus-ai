using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;

internal static class BaselineForecasts
{
    internal static readonly string[] CandidateNames =
    {
        "NaiveLastScore",
        "MovingAverage3",
        "LinearRegression",
        "Holt",
        "KNN"
    };

    internal static double Predict(
        string modelName,
        IReadOnlyList<SessionFeatures> history,
        int horizon = 1,
        int windowSize = 12,
        int knnK = 3,
        double holtAlpha = 0.45,
        double holtBeta = 0.30)
    {
        List<SessionFeatures> ordered = NextSessionFeatureBuilder.OrderAndValidate(history);
        if (ordered.Count == 0)
            return 0;

        horizon = Math.Max(1, horizon);
        double[] scores = ordered.Select(s => s.Score).ToArray();

        double result = modelName switch
        {
            "NaiveLastScore" => scores[^1],
            "MovingAverage3" => scores.TakeLast(Math.Min(3, scores.Length)).Average(),
            "LinearRegression" => LinearRegressionForecast(scores, horizon, windowSize),
            "Holt" => HoltForecast(scores, horizon, windowSize, holtAlpha, holtBeta),
            "KNN" => KnnForecast(ordered, knnK),
            _ => HoltForecast(scores, horizon, windowSize, holtAlpha, holtBeta)
        };

        return Math.Clamp(double.IsFinite(result) ? result : scores[^1], 0, 100);
    }

    internal static (double intercept, double slope) LinearRegressionFit(IReadOnlyList<double> values)
    {
        int n = values.Count;
        if (n == 0)
            return (0, 0);
        if (n == 1)
            return (values[0], 0);

        double xMean = (n - 1) / 2.0;
        double yMean = values.Average();
        double ssXY = 0, ssXX = 0;
        for (int i = 0; i < n; i++)
        {
            ssXY += (i - xMean) * (values[i] - yMean);
            ssXX += (i - xMean) * (i - xMean);
        }

        double slope = ssXX > 1e-12 ? ssXY / ssXX : 0;
        return (yMean - slope * xMean, slope);
    }

    internal static double LinearRegressionForecast(double[] allScores, int horizon, int windowSize)
    {
        double[] scores = allScores.TakeLast(Math.Min(allScores.Length, Math.Max(2, windowSize))).ToArray();
        if (scores.Length == 0)
            return 0;
        var (intercept, slope) = LinearRegressionFit(scores);
        return Math.Clamp(intercept + slope * (scores.Length - 1 + horizon), 0, 100);
    }

    internal static double HoltForecast(
        double[] allScores,
        int horizon,
        int windowSize,
        double alpha,
        double beta)
    {
        double[] scores = allScores.TakeLast(Math.Min(allScores.Length, Math.Max(2, windowSize))).ToArray();
        if (scores.Length == 0)
            return 0;
        if (scores.Length == 1)
            return scores[0];

        alpha = Math.Clamp(alpha, 0.01, 0.99);
        beta = Math.Clamp(beta, 0.01, 0.99);

        double level = scores[0];
        double trend = scores[1] - scores[0];
        for (int i = 1; i < scores.Length; i++)
        {
            double previousLevel = level;
            level = alpha * scores[i] + (1 - alpha) * (level + trend);
            trend = beta * (level - previousLevel) + (1 - beta) * trend;
        }

        
        double perStepTrend = Math.Clamp(trend, -15, 15);
        return Math.Clamp(level + perStepTrend * horizon, 0, 100);
    }

    private static double KnnForecast(IReadOnlyList<SessionFeatures> history, int k)
    {
        if (history.Count < 3)
            return history[^1].Score;

        NextSessionRow query = NextSessionFeatureBuilder.BuildForNextSession(history);
        List<NextSessionTrainingSample> candidates = NextSessionFeatureBuilder.BuildTrainingSamples(history);
        if (candidates.Count == 0)
            return history[^1].Score;

        
        
        candidates = candidates.TakeLast(100).ToList();
        float[] queryVector = query.ToFeatureArray();
        var candidateVectors = candidates.Select(c => c.Row.ToFeatureArray()).ToList();
        int dimensions = queryVector.Length;
        var lows = new float[dimensions];
        var highs = new float[dimensions];

        for (int d = 0; d < dimensions; d++)
        {
            float min = queryVector[d];
            float max = queryVector[d];
            foreach (float[] vector in candidateVectors)
            {
                min = Math.Min(min, vector[d]);
                max = Math.Max(max, vector[d]);
            }
            lows[d] = min;
            highs[d] = max;
        }

        var nearest = candidates
            .Select((sample, i) => new
            {
                sample.TargetScore,
                Distance = NormalizedDistance(queryVector, candidateVectors[i], lows, highs)
            })
            .OrderBy(x => x.Distance)
            .Take(Math.Clamp(k, 1, candidates.Count))
            .ToList();

        double weightSum = nearest.Sum(x => 1.0 / (x.Distance + 1e-6));
        if (weightSum <= 0 || !double.IsFinite(weightSum))
            return nearest.Average(x => x.TargetScore);

        return nearest.Sum(x => x.TargetScore / (x.Distance + 1e-6)) / weightSum;
    }

    private static double NormalizedDistance(float[] a, float[] b, float[] lows, float[] highs)
    {
        double sumSquares = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double range = highs[i] - lows[i];
            if (range < 1e-9)
                continue;
            double diff = (a[i] - lows[i]) / range - (b[i] - lows[i]) / range;
            sumSquares += diff * diff;
        }
        return Math.Sqrt(sumSquares);
    }
}
