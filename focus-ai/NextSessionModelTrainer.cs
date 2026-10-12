using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using focus_ai.Prediction.Models;
using System.IO;
namespace focus_ai.Prediction;
public static class NextSessionModelTrainer
{
    private const double CalibrationLevel = 0.90;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static TrainingReport TrainAndSave(
        IReadOnlyList<SessionFeatures> history,
        string modelPath,
        PredictionTrainingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Calea modelului nu poate fi goală.", nameof(modelPath));

        options ??= new PredictionTrainingOptions();
        List<NextSessionTrainingSample> samples = NextSessionFeatureBuilder.BuildTrainingSamples(history);
        int count = samples.Count;

        if (count < options.MinimumExamplesForBacktesting)
        {
            return new TrainingReport
            {
                Success = false,
                TotalExamples = count,
                Message = $"Sunt disponibile doar {count} exemple supravegheate. Pentru backtesting sunt necesare cel puțin {options.MinimumExamplesForBacktesting}. " +
                          "Până atunci, PredictionEngine poate utiliza automat modelele statistice de bază."
            };
        }

        // Chronological partitions: 60% train, next 15% validation, next 10% calibration, last 15% test.
        int trainEnd = (int)Math.Floor(count * 0.60);
        int validationEnd = (int)Math.Floor(count * 0.75);
        int calibrationEnd = (int)Math.Floor(count * 0.85);

        if (trainEnd < 5 || validationEnd <= trainEnd || calibrationEnd <= validationEnd || count <= calibrationEnd)
        {
            return new TrainingReport
            {
                Success = false,
                TotalExamples = count,
                Message = "Nu există suficiente exemple pentru împărțirea cronologică train/validation/calibration/test."
            };
        }

        List<NextSessionTrainingSample> trainSamples = samples.Take(trainEnd).ToList();
        List<NextSessionTrainingSample> validationSamples = samples.Skip(trainEnd).Take(validationEnd - trainEnd).ToList();
        List<NextSessionTrainingSample> calibrationSamples = samples.Skip(validationEnd).Take(calibrationEnd - validationEnd).ToList();
        List<NextSessionTrainingSample> testSamples = samples.Skip(calibrationEnd).ToList();

        var ml = new MLContext(seed: 42);
        var candidateValidationMae = new Dictionary<string, double>(StringComparer.Ordinal);
        var mlModels = new Dictionary<string, ITransformer>(StringComparer.Ordinal);
        // Baselines are evaluated prequentially: each validation example only sees its own prefix.
        foreach (string baselineName in BaselineForecasts.CandidateNames)
        {
            double[] predictions = validationSamples
                .Select(s => BaselineForecasts.Predict(baselineName, s.HistoryBeforeTarget))
                .ToArray();
            candidateValidationMae[baselineName] = CalculateMetrics(
                validationSamples.Select(s => s.TargetScore).ToArray(), predictions).Mae;
        }

        // Only attempt learned models after there is enough data to make the comparison meaningful.
        if (count >= options.MinimumExamplesForSdca && trainSamples.Count >= 20)
        {
            TryAddMlCandidate(
                "ML.NET SDCA",
                BuildSdcaPipeline(ml),
                trainSamples,
                validationSamples,
                ml,
                mlModels,
                candidateValidationMae);
        }

        if (trainSamples.Count >= options.MinimumTrainingRowsForLightGbm)
        {
            int leafSize = Math.Max(2, Math.Min(options.MinimumLightGbmLeafSize, trainSamples.Count / 8));
            TryAddMlCandidate(
                "ML.NET LightGBM",
                BuildLightGbmPipeline(ml, options, leafSize),
                trainSamples,
                validationSamples,
                ml,
                mlModels,
                candidateValidationMae);
        }

        string bestBaseline = BaselineForecasts.CandidateNames
            .OrderBy(name => candidateValidationMae.GetValueOrDefault(name, double.PositiveInfinity))
            .First();
        double bestBaselineMae = candidateValidationMae[bestBaseline];

        string selectedModel = bestBaseline;
        double selectedValidationMae = bestBaselineMae;

        // Use an ML model only when it beats the best baseline by a minimum margin.
        var bestMl = mlModels.Keys
            .Select(name => (Name: name, Mae: candidateValidationMae.GetValueOrDefault(name, double.PositiveInfinity)))
            .OrderBy(x => x.Mae)
            .FirstOrDefault();

        if (bestMl.Name is not null &&
            double.IsFinite(bestMl.Mae) &&
            bestMl.Mae < bestBaselineMae - Math.Max(0, options.MinimumMlMaeImprovement))
        {
            selectedModel = bestMl.Name;
            selectedValidationMae = bestMl.Mae;
        }

        ITransformer? finalModel = null;
        IDataView? finalTrainingData = null;
        if (IsMlModel(selectedModel))
        {
            // Retrain only on data strictly before the calibration partition.
            List<NextSessionRow> finalRows = samples.Take(validationEnd).Select(s => s.Row).ToList();
            finalTrainingData = ml.Data.LoadFromEnumerable(finalRows);
            IEstimator<ITransformer> finalPipeline = BuildPipelineForModel(ml, selectedModel, options, finalRows.Count);
            finalModel = finalPipeline.Fit(finalTrainingData);
        }

        double[] calibrationPredictions = PredictSamples(selectedModel, finalModel, ml, calibrationSamples);
        double[] calibrationLabels = calibrationSamples.Select(s => s.TargetScore).ToArray();
        double[] calibrationErrors = calibrationLabels
            .Zip(calibrationPredictions, (actual, predicted) => Math.Abs(actual - predicted))
            .ToArray();

        double? quantile90 = calibrationErrors.Length >= options.MinimumCalibrationExamples
            ? FiniteSampleQuantile(calibrationErrors, CalibrationLevel)
            : null;

        double[] testPredictions = PredictSamples(selectedModel, finalModel, ml, testSamples);
        double[] testLabels = testSamples.Select(s => s.TargetScore).ToArray();
        RegressionSummary testMetrics = CalculateMetrics(testLabels, testPredictions);

        double? intervalCoverage = quantile90.HasValue && testLabels.Length > 0
            ? testLabels.Zip(testPredictions, (actual, predicted) => Math.Abs(actual - predicted) <= quantile90.Value)
                .Count(covered => covered) / (double)testLabels.Length
            : null;

        string fullModelPath = Path.GetFullPath(modelPath);
        string? directory = Path.GetDirectoryName(fullModelPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (IsMlModel(selectedModel))
        {
            if (finalModel is null || finalTrainingData is null)
                throw new InvalidOperationException("Modelul ML selectat nu a fost antrenat.");

            string temporaryPath = fullModelPath + ".tmp";
            ml.Model.Save(finalModel, finalTrainingData.Schema, temporaryPath);
            File.Move(temporaryPath, fullModelPath, overwrite: true);
        }
        else if (File.Exists(fullModelPath))
        {
            // Prevent an old ML zip from being mistaken for a current baseline artifact.
            File.Delete(fullModelPath);
        }

        var metadata = new PredictionModelMetadata
        {
            SelectedModel = selectedModel,
            TrainedAtUtc = DateTime.UtcNow,
            TrainingExamples = validationEnd,
            CalibrationExamples = calibrationSamples.Count,
            TestExamples = testSamples.Count,
            ValidationMae = selectedValidationMae,
            TestMae = testMetrics.Mae,
            TestRmse = testMetrics.Rmse,
            TestRSquared = testMetrics.RSquared,
            CalibrationQuantile90 = quantile90,
            TestIntervalCoverage90 = intervalCoverage
        };

        string metadataPath = fullModelPath + ".meta.json";
        string metadataTempPath = metadataPath + ".tmp";
        File.WriteAllText(metadataTempPath, JsonSerializer.Serialize(metadata, JsonOptions));
        File.Move(metadataTempPath, metadataPath, overwrite: true);

        string calibrationMessage = quantile90.HasValue
            ? $"Intervalul empiric nominal 90% a fost calibrat cu {calibrationSamples.Count} exemple; acoperirea pe test este " +
              (intervalCoverage.HasValue ? $"{intervalCoverage.Value:P0}." : "neevaluată.")
            : $"Nu există suficiente exemple de calibrare pentru un interval nominal 90% (minim {options.MinimumCalibrationExamples}).";

        return new TrainingReport
        {
            Success = true,
            Message = $"Antrenare și evaluare finalizate. {calibrationMessage}",
            SelectedModel = selectedModel,
            TotalExamples = count,
            TrainingExamples = validationEnd,
            ValidationExamples = validationSamples.Count,
            CalibrationExamples = calibrationSamples.Count,
            TestExamples = testSamples.Count,
            ValidationMae = selectedValidationMae,
            TestMae = testMetrics.Mae,
            TestRmse = testMetrics.Rmse,
            TestRSquared = testMetrics.RSquared,
            CalibrationQuantile90 = quantile90,
            TestIntervalCoverage90 = intervalCoverage,
            ModelPath = fullModelPath
        };
    }

    internal static bool IsMlModel(string modelName) =>
        string.Equals(modelName, "ML.NET SDCA", StringComparison.Ordinal) ||
        string.Equals(modelName, "ML.NET LightGBM", StringComparison.Ordinal);

    internal static double PredictWithModel(MLContext ml, ITransformer model, NextSessionRow row)
    {
        IDataView input = ml.Data.LoadFromEnumerable(new[] { row });
        IDataView transformed = model.Transform(input);
        ScoreOutput output = ml.Data.CreateEnumerable<ScoreOutput>(transformed, reuseRowObject: false).Single();
        return Math.Clamp(double.IsFinite(output.Score) ? output.Score : row.ScoreLast, 0, 100);
    }

    private static void TryAddMlCandidate(
        string name,
        IEstimator<ITransformer> pipeline,
        IReadOnlyList<NextSessionTrainingSample> trainSamples,
        IReadOnlyList<NextSessionTrainingSample> validationSamples,
        MLContext ml,
        IDictionary<string, ITransformer> models,
        IDictionary<string, double> validationMae)
    {
        try
        {
            IDataView trainData = ml.Data.LoadFromEnumerable(trainSamples.Select(s => s.Row));
            ITransformer model = pipeline.Fit(trainData);
            double[] predictions = PredictWithModel(ml, model, validationSamples.Select(s => s.Row).ToList());
            double mae = CalculateMetrics(validationSamples.Select(s => s.TargetScore).ToArray(), predictions).Mae;
            if (double.IsFinite(mae))
            {
                models[name] = model;
                validationMae[name] = mae;
            }
        }
        catch (Exception ex)
        {
            // An ML candidate failing to train must not prevent the well-defined baselines from working.
            Console.Error.WriteLine($"Candidatul {name} nu a putut fi antrenat și va fi ignorat: {ex.Message}");
        }
    }

    private static double[] PredictWithModel(MLContext ml, ITransformer model, IReadOnlyList<NextSessionRow> rows)
    {
        if (rows.Count == 0)
            return Array.Empty<double>();

        IDataView input = ml.Data.LoadFromEnumerable(rows);
        IDataView transformed = model.Transform(input);
        return ml.Data.CreateEnumerable<ScoreOutput>(transformed, reuseRowObject: false)
            .Select<ScoreOutput, double>(o => Math.Clamp(double.IsFinite(o.Score) ? o.Score : 0, 0, 100))
            .ToArray<double>();
    }

    private static double[] PredictSamples(
        string modelName,
        ITransformer? model,
        MLContext ml,
        IReadOnlyList<NextSessionTrainingSample> samples)
    {
        if (samples.Count == 0)
            return Array.Empty<double>();

        if (IsMlModel(modelName))
        {
            if (model is null)
                throw new InvalidOperationException($"Modelul {modelName} lipsește.");
            return PredictWithModel(ml, model, samples.Select(s => s.Row).ToList());
        }

        return samples
            .Select(s => BaselineForecasts.Predict(modelName, s.HistoryBeforeTarget))
            .ToArray();
    }

    private static IEstimator<ITransformer> BuildPipelineForModel(
        MLContext ml,
        string modelName,
        PredictionTrainingOptions options,
        int trainingRows)
    {
        if (modelName == "ML.NET SDCA")
            return BuildSdcaPipeline(ml);

        int leafSize = Math.Max(2, Math.Min(options.MinimumLightGbmLeafSize, trainingRows / 8));
        return BuildLightGbmPipeline(ml, options, leafSize);
    }

    private static IEstimator<ITransformer> BuildSdcaPipeline(MLContext ml)
    {
        return ml.Transforms
            .Concatenate("Features", NextSessionRow.FeatureColumns)
            .Append(ml.Transforms.NormalizeMeanVariance("Features"))
            .Append(ml.Regression.Trainers.Sdca(
                labelColumnName: "Label",
                featureColumnName: "Features"));
    }

    private static IEstimator<ITransformer> BuildLightGbmPipeline(
        MLContext ml,
        PredictionTrainingOptions options,
        int leafSize)
    {
        return ml.Transforms
            .Concatenate("Features", NextSessionRow.FeatureColumns)
            .Append(ml.Regression.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: Math.Max(3, options.LightGbmLeaves),
                minimumExampleCountPerLeaf: Math.Max(2, leafSize),
                learningRate: Math.Clamp(options.LightGbmLearningRate, 0.001, 0.5),
                numberOfIterations: Math.Clamp(options.LightGbmIterations, 10, 2000)));
    }

    private static RegressionSummary CalculateMetrics(double[] labels, double[] predictions)
    {
        if (labels.Length == 0 || labels.Length != predictions.Length)
            return new RegressionSummary(double.PositiveInfinity, double.PositiveInfinity, 0);

        double mae = 0, squaredError = 0;
        double labelMean = labels.Average();
        double totalSumSquares = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            double error = labels[i] - predictions[i];
            mae += Math.Abs(error);
            squaredError += error * error;
            double centered = labels[i] - labelMean;
            totalSumSquares += centered * centered;
        }

        mae /= labels.Length;
        double rmse = Math.Sqrt(squaredError / labels.Length);
        double rSquared = totalSumSquares > 1e-12 ? 1 - squaredError / totalSumSquares : 0;
        return new RegressionSummary(mae, rmse, rSquared);
    }

    private static double FiniteSampleQuantile(IEnumerable<double> values, double probability)
    {
        double[] sorted = values.Where(double.IsFinite).OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
            return 0;

        probability = Math.Clamp(probability, 0.01, 0.99);
        // Finite-sample order statistic used by split-conformal regression intervals.
        int oneBasedIndex = (int)Math.Ceiling((sorted.Length + 1) * probability);
        int index = Math.Clamp(oneBasedIndex - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private sealed record RegressionSummary(double Mae, double Rmse, double RSquared);
}

/// <summary>Public ML.NET output DTO used when enumerating transformed rows.</summary>
public sealed class ScoreOutput
{
    public ScoreOutput() { }

    [ColumnName("Label")]
    public float Label { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}
