using focus_ai;
using Xunit;

namespace FocusAI.Tests;

public sealed class TestScoreCalculatorTests
{
    [Fact]
    public void EstimateAttention_WidensUncertaintyWhenOnlySensorsAreMissing()
    {
        AttentionScoreResult noSensors = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput());
        AttentionScoreResult observed = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput
        {
            MapData = "10,10;20,20;30,30;40,40;50,50;60,60",
            CptAccuracyPercent = 90,
            CptHitRate = 0.9,
            CptFalseAlarmRate = 0.05,
            CptTauMs = 100,
            CptReactionTimeCv = 0.15,
            CptReactionTimeSampleCount = 12
        });

        Assert.Equal(0, noSensors.Posterior.ObservationCount);
        Assert.Equal(2, observed.Posterior.ObservationCount);
        Assert.Contains(observed.Posterior.Updates, update => update.Source == "cpt-balanced-performance");
        Assert.Contains(observed.Posterior.Updates, update => update.Source == "cpt-rt-tail-variability");
        Assert.True(observed.Posterior.PosteriorStandardDeviation < noSensors.Posterior.PosteriorStandardDeviation);
        Assert.True(observed.TauSubscore is > 0.5);
    }

    [Fact]
    public void AnalyzeGaze_LegacyCoordinatePayloadIsNotTreatedAsAttentionEvidence()
    {
        GazeSummary gaze = TestScoreCalculator.AnalyzeGaze("10,10;20,20;30,30;40,40;50,50;60,60");
        GazeSummary missing = TestScoreCalculator.AnalyzeGaze(null);

        Assert.False(gaze.IsAvailable);
        Assert.Equal(6, gaze.SampleCount);
        Assert.False(missing.IsAvailable);
    }

    [Fact]
    public void EstimateAttention_UsesExplicitTargetAoiRatherThanMapValidity()
    {
        AttentionScoreResult result = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput
        {
            MapData = "10,10;20,20;30,30;40,40;50,50",
            GazeOnTargetFraction = 0.72,
            GazeTargetSampleCount = 90,
            GazeTargetTrackingCoverage = 0.80,
            GazeValidFraction = 0.75
        });

        Assert.Contains(result.Posterior.Updates, update => update.Source == "gaze-on-target");
        Assert.Equal(0.72, result.GazeOnTargetFraction);
        Assert.Equal(0.75, result.GazeValidFraction);
    }

    [Fact]
    public void EstimateAttention_KeepsPpgPrvDescriptiveButDoesNotFuseItAsCalibratedAttention()
    {
        HrvMetricsResult hrv = HrvMetrics.Analyze(Enumerable.Range(0, 72)
            .Select(index => 825d + 38 * Math.Sin(index * 0.4) + (index % 5 - 2) * 8));

        AttentionScoreResult result = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput { Hrv = hrv });

        Assert.True(hrv.IsAvailable);
        Assert.NotNull(result.HrvAdaptivitySubscore);
        Assert.Empty(result.Posterior.Updates);
    }

}
