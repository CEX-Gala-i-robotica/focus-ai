using focus_ai;
namespace focus_ai.Prediction;

public sealed class PredictionModelMetadata
{
    public int ArtifactVersion { get; set; } = 2;
    public string SelectedModel { get; set; } = string.Empty;
    public DateTime TrainedAtUtc { get; set; } = DateTime.UtcNow;
    public int TrainingExamples { get; set; }
    public int ValidationExamples { get; set; }
    public int CalibrationExamples { get; set; }
    public int TestExamples { get; set; }
    public double ValidationMae { get; set; }
    public double TestMae { get; set; }
    public double TestRmse { get; set; }
    public double TestRSquared { get; set; }
    public double? CalibrationQuantile90 { get; set; }
    public double? TestIntervalCoverage90 { get; set; }
    public string FeatureSchemaVersion { get; set; } = "next-session-v2";
    public string DataFingerprint { get; set; } = string.Empty;
    public int ScoreFormulaVersion { get; set; } = TestScoreCalculator.CurrentVersion;
}

public sealed record TrainingReport
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SelectedModel { get; init; } = string.Empty;
    public int TotalExamples { get; init; }
    public int TrainingExamples { get; init; }
    public int ValidationExamples { get; init; }
    public int CalibrationExamples { get; init; }
    public int TestExamples { get; init; }
    public double ValidationMae { get; init; }
    public double TestMae { get; init; }
    public double TestRmse { get; init; }
    public double TestRSquared { get; init; }
    public double? CalibrationQuantile90 { get; init; }
    public double? TestIntervalCoverage90 { get; init; }
    public string? ModelPath { get; init; }
}

public sealed class PredictionTrainingOptions
{
    public int MinimumExamplesForBacktesting { get; init; } = 70;
    public int MinimumExamplesForSdca { get; init; } = 100;
    public int MinimumTrainingRowsForLightGbm { get; init; } = 80;
    public int MinimumCalibrationExamples { get; init; } = 9;
    public int MinimumLightGbmLeafSize { get; init; } = 8;
    public int LightGbmLeaves { get; init; } = 5;
    public int LightGbmIterations { get; init; } = 100;
    public double LightGbmLearningRate { get; init; } = 0.03;
    public double MinimumMlMaeImprovement { get; init; } = 0.25;
}
