namespace focus_ai.Prediction;

/// <summary>Metadata stored next to the model file. It does not contain raw user sessions.</summary>
public sealed class PredictionModelMetadata
{
    public int ArtifactVersion { get; set; } = 1;
    public string SelectedModel { get; set; } = "";
    public DateTime TrainedAtUtc { get; set; } = DateTime.UtcNow;
    public int TrainingExamples { get; set; }
    public int CalibrationExamples { get; set; }
    public int TestExamples { get; set; }
    public double ValidationMae { get; set; }
    public double TestMae { get; set; }
    public double TestRmse { get; set; }
    public double TestRSquared { get; set; }
    public double? CalibrationQuantile90 { get; set; }
    public double? TestIntervalCoverage90 { get; set; }
    public string FeatureSchemaVersion { get; set; } = "next-session-v1";
}

public sealed record TrainingReport
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string SelectedModel { get; init; } = "";
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
    /// <summary>Minimum samples needed for temporal backtesting and writing metadata.</summary>
    public int MinimumExamplesForBacktesting { get; init; } = 25;
    /// <summary>SDCA is considered only after this many supervised examples exist.</summary>
    public int MinimumExamplesForSdca { get; init; } = 40;
    /// <summary>LightGBM is considered only when the first training partition is this large.</summary>
    public int MinimumTrainingRowsForLightGbm { get; init; } = 35;
    public int MinimumCalibrationExamples { get; init; } = 5;
    public int MinimumLightGbmLeafSize { get; init; } = 4;
    public int LightGbmLeaves { get; init; } = 7;
    public int LightGbmIterations { get; init; } = 150;
    public double LightGbmLearningRate { get; init; } = 0.05;
    /// <summary>Require ML to beat the best simple baseline by this many score points.</summary>
    public double MinimumMlMaeImprovement { get; init; } = 0.25;
}
