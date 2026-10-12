using Microsoft.ML.Data;

namespace focus_ai.Prediction;

/// <summary>
/// One supervised example: features from sessions through t, label = score at t+1.
/// Keep this shape stable after training, because the saved ML.NET model expects it.
/// </summary>
public sealed class NextSessionRow
{
    [ColumnName("Label")]
    public float Label { get; set; }

    public float ScoreLast { get; set; }
    public float ScoreLag1 { get; set; }
    public float ScoreLag2 { get; set; }
    public float ScoreMean3 { get; set; }
    public float ScoreStd3 { get; set; }
    public float ScoreDelta1 { get; set; }
    public float ScoreDelta2 { get; set; }
    public float ScoreTrend3 { get; set; }
    public float HistoryCount { get; set; }

    public float Tr2 { get; set; }
    public float HasTr2Data { get; set; }
    public float PrecizieGonogo { get; set; }
    public float HasPrecizieGonogoData { get; set; }

    public float HrMean { get; set; }
    public float HrStd { get; set; }
    public float HrRange { get; set; }
    public float HrHasData { get; set; }
    public float HrSampleCount { get; set; }

    public float Spo2Mean { get; set; }
    public float Spo2Min { get; set; }
    public float Spo2DropCount { get; set; }
    public float Spo2HasData { get; set; }
    public float Spo2SampleCount { get; set; }

    public float DistRatio { get; set; }
    public float DistHasData { get; set; }
    public float DistSampleCount { get; set; }

    public float EcgDrMean { get; set; }
    public float EcgStMean { get; set; }
    public float EcgRange { get; set; }
    public float EcgHasData { get; set; }
    public float EcgSampleCount { get; set; }

    public float MapXVar { get; set; }
    public float MapYVar { get; set; }
    public float MapTotalPath { get; set; }
    public float MapHasData { get; set; }
    public float MapSampleCount { get; set; }

    public float DurationMinutes { get; set; }
    public float DaysSincePrevious { get; set; }
    public float HasTimeGapData { get; set; }
    public float SessionHourSin { get; set; }
    public float SessionHourCos { get; set; }
    public float DayOfWeekSin { get; set; }
    public float DayOfWeekCos { get; set; }
    public float HasTimestampData { get; set; }

    public static readonly string[] FeatureColumns =
    {
        nameof(ScoreLast), nameof(ScoreLag1), nameof(ScoreLag2), nameof(ScoreMean3),
        nameof(ScoreStd3), nameof(ScoreDelta1), nameof(ScoreDelta2), nameof(ScoreTrend3),
        nameof(HistoryCount), nameof(Tr2), nameof(HasTr2Data), nameof(PrecizieGonogo),
        nameof(HasPrecizieGonogoData), nameof(HrMean), nameof(HrStd), nameof(HrRange),
        nameof(HrHasData), nameof(HrSampleCount), nameof(Spo2Mean), nameof(Spo2Min),
        nameof(Spo2DropCount), nameof(Spo2HasData), nameof(Spo2SampleCount), nameof(DistRatio),
        nameof(DistHasData), nameof(DistSampleCount), nameof(EcgDrMean), nameof(EcgStMean),
        nameof(EcgRange), nameof(EcgHasData), nameof(EcgSampleCount), nameof(MapXVar),
        nameof(MapYVar), nameof(MapTotalPath), nameof(MapHasData), nameof(MapSampleCount),
        nameof(DurationMinutes), nameof(DaysSincePrevious), nameof(HasTimeGapData),
        nameof(SessionHourSin), nameof(SessionHourCos), nameof(DayOfWeekSin),
        nameof(DayOfWeekCos), nameof(HasTimestampData)
    };

    /// <summary>Returns the feature vector in exactly the same order as FeatureColumns.</summary>
    public float[] ToFeatureArray() => new[]
    {
        ScoreLast, ScoreLag1, ScoreLag2, ScoreMean3, ScoreStd3, ScoreDelta1,
        ScoreDelta2, ScoreTrend3, HistoryCount, Tr2, HasTr2Data, PrecizieGonogo,
        HasPrecizieGonogoData, HrMean, HrStd, HrRange, HrHasData, HrSampleCount,
        Spo2Mean, Spo2Min, Spo2DropCount, Spo2HasData, Spo2SampleCount, DistRatio,
        DistHasData, DistSampleCount, EcgDrMean, EcgStMean, EcgRange, EcgHasData,
        EcgSampleCount, MapXVar, MapYVar, MapTotalPath, MapHasData, MapSampleCount,
        DurationMinutes, DaysSincePrevious, HasTimeGapData, SessionHourSin,
        SessionHourCos, DayOfWeekSin, DayOfWeekCos, HasTimestampData
    };
}

internal sealed record NextSessionTrainingSample(
    NextSessionRow Row,
    IReadOnlyList<focus_ai.Prediction.Models.SessionFeatures> HistoryBeforeTarget,
    double TargetScore);
