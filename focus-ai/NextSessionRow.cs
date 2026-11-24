using Microsoft.ML.Data;

namespace focus_ai.Prediction;





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
    public float CptMuMs { get; set; }
    public float CptSigmaMs { get; set; }
    public float CptTauMs { get; set; }
    public float CptReactionTimeCv { get; set; }
    public float CptReactionTimeSampleCount { get; set; }
    public float CptHasExGaussianData { get; set; }

    public float HrMean { get; set; }
    public float HrStd { get; set; }
    public float HrRange { get; set; }
    public float HrHasData { get; set; }
    public float HrSamplesPerMinute { get; set; }

    public float Spo2Mean { get; set; }
    public float Spo2Min { get; set; }
    public float Spo2DropRate { get; set; }
    public float Spo2HasData { get; set; }
    public float Spo2SamplesPerMinute { get; set; }

    public float DistRatio { get; set; }
    public float DistHasData { get; set; }
    public float DistReadingsPerMinute { get; set; }

    public float EcgDrMean { get; set; }
    public float EcgStMean { get; set; }
    public float EcgRange { get; set; }
    public float EcgHasData { get; set; }
    public float EcgPairsPerMinute { get; set; }

    public float HrvSdnnMs { get; set; }
    public float HrvRmssdMs { get; set; }
    public float HrvDfaAlpha1 { get; set; }
    public float HrvSampleEntropy { get; set; }
    public float RrSampleCount { get; set; }
    public float HrvHasData { get; set; }

    public float MapXVar { get; set; }
    public float MapYVar { get; set; }
    public float MapTotalPath { get; set; }
    public float MapHasData { get; set; }
    public float MapPointsPerMinute { get; set; }

    public float AttentionEstimate { get; set; }
    public float AttentionPosteriorStandardDeviation { get; set; }
    public float AttentionObservationCount { get; set; }
    public float AttentionHasData { get; set; }

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
        nameof(HasPrecizieGonogoData), nameof(CptMuMs), nameof(CptSigmaMs), nameof(CptTauMs),
        nameof(CptReactionTimeCv), nameof(CptReactionTimeSampleCount), nameof(CptHasExGaussianData),
        nameof(HrMean), nameof(HrStd), nameof(HrRange),
        nameof(HrHasData), nameof(HrSamplesPerMinute), nameof(Spo2Mean), nameof(Spo2Min),
        nameof(Spo2DropRate), nameof(Spo2HasData), nameof(Spo2SamplesPerMinute), nameof(DistRatio),
        nameof(DistHasData), nameof(DistReadingsPerMinute), nameof(EcgDrMean), nameof(EcgStMean),
        nameof(EcgRange), nameof(EcgHasData), nameof(EcgPairsPerMinute), nameof(MapXVar),
        nameof(HrvSdnnMs), nameof(HrvRmssdMs), nameof(HrvDfaAlpha1), nameof(HrvSampleEntropy),
        nameof(RrSampleCount), nameof(HrvHasData), nameof(MapYVar), nameof(MapTotalPath), nameof(MapHasData),
        nameof(MapPointsPerMinute), nameof(AttentionEstimate), nameof(AttentionPosteriorStandardDeviation),
        nameof(AttentionObservationCount), nameof(AttentionHasData),
        nameof(DurationMinutes), nameof(DaysSincePrevious), nameof(HasTimeGapData),
        nameof(SessionHourSin), nameof(SessionHourCos), nameof(DayOfWeekSin),
        nameof(DayOfWeekCos), nameof(HasTimestampData)
    };

    
    public float[] ToFeatureArray() => new[]
    {
        ScoreLast, ScoreLag1, ScoreLag2, ScoreMean3, ScoreStd3, ScoreDelta1,
        ScoreDelta2, ScoreTrend3, HistoryCount, Tr2, HasTr2Data, PrecizieGonogo,
        HasPrecizieGonogoData, CptMuMs, CptSigmaMs, CptTauMs, CptReactionTimeCv,
        CptReactionTimeSampleCount, CptHasExGaussianData, HrMean, HrStd, HrRange, HrHasData, HrSamplesPerMinute,
        Spo2Mean, Spo2Min, Spo2DropRate, Spo2HasData, Spo2SamplesPerMinute, DistRatio,
        DistHasData, DistReadingsPerMinute, EcgDrMean, EcgStMean, EcgRange, EcgHasData,
        EcgPairsPerMinute, MapXVar, HrvSdnnMs, HrvRmssdMs, HrvDfaAlpha1, HrvSampleEntropy,
        RrSampleCount, HrvHasData, MapYVar, MapTotalPath, MapHasData, MapPointsPerMinute,
        AttentionEstimate, AttentionPosteriorStandardDeviation, AttentionObservationCount, AttentionHasData,
        DurationMinutes, DaysSincePrevious, HasTimeGapData, SessionHourSin,
        SessionHourCos, DayOfWeekSin, DayOfWeekCos, HasTimestampData
    };
}

internal sealed record NextSessionTrainingSample(
    NextSessionRow Row,
    IReadOnlyList<focus_ai.Prediction.Models.SessionFeatures> HistoryBeforeTarget,
    double TargetScore);
