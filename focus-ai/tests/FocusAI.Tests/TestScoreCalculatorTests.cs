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
        Assert.True(observed.Posterior.ObservationCount >= 2);
        Assert.True(observed.Posterior.PosteriorStandardDeviation < noSensors.Posterior.PosteriorStandardDeviation);
        Assert.True(observed.TauSubscore is > 0.5);
    }

    [Fact]
    public void AnalyzeGaze_DistinguishesValidGazeFractionFromMissingData()
    {
        GazeSummary gaze = TestScoreCalculator.AnalyzeGaze("10,10;bad,2;20,20;150,2;30,30;40,40");
        GazeSummary missing = TestScoreCalculator.AnalyzeGaze(null);

        Assert.True(gaze.IsAvailable);
        Assert.Equal(6, gaze.SampleCount);
        Assert.Equal(4, gaze.ValidSampleCount);
        Assert.Equal(4d / 6d, gaze.OnScreenFraction, precision: 6);
        Assert.False(missing.IsAvailable);
    }

    [Fact]
    public void EstimateAttention_UsesHrvOnlyWhenActualRrMetricsAreAvailable()
    {
        HrvMetricsResult hrv = HrvMetrics.Analyze(Enumerable.Range(0, 72)
            .Select(index => 825d + 38 * Math.Sin(index * 0.4) + (index % 5 - 2) * 8));

        AttentionScoreResult result = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput { Hrv = hrv });

        Assert.True(hrv.IsAvailable);
        Assert.NotNull(result.HrvAdaptivitySubscore);
        Assert.Single(result.Posterior.Updates);
        Assert.Equal("hrv-adaptivity", result.Posterior.Updates[0].Source);
    }
}
