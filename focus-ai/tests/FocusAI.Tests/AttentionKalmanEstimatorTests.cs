using focus_ai;
using Xunit;

namespace FocusAI.Tests;

public sealed class AttentionKalmanEstimatorTests
{
    [Fact]
    public void Estimate_UsesPreciseObservationAndReducesUncertainty()
    {
        var estimator = new AttentionKalmanEstimator();
        AttentionEstimate priorOnly = estimator.Estimate(null);
        AttentionEstimate posterior = estimator.Estimate(new[]
        {
            new AttentionObservation("CPT", 0.90, 0.02),
            new AttentionObservation("gaze", 0.85, 0.04)
        });

        Assert.Equal(0, priorOnly.ObservationCount);
        Assert.Equal(2, posterior.ObservationCount);
        Assert.True(posterior.Estimate > 0.80);
        Assert.True(posterior.PosteriorStandardDeviation < priorOnly.PosteriorStandardDeviation);
        Assert.InRange(posterior.ConfidenceLow, 0, posterior.Estimate);
        Assert.InRange(posterior.ConfidenceHigh, posterior.Estimate, 1);
    }

    [Fact]
    public void Estimate_IgnoresMissingOrInvalidSensors()
    {
        AttentionEstimate result = new AttentionKalmanEstimator().Estimate(new[]
        {
            new AttentionObservation("", 0.9, 0.1),
            new AttentionObservation("bad-value", double.NaN, 0.1),
            new AttentionObservation("bad-variance", 0.9, 0),
            new AttentionObservation("valid", 1.4, 0.05)
        });

        Assert.Single(result.Updates);
        Assert.Equal("valid", result.Updates[0].Source);
        Assert.InRange(result.Estimate, 0, 1);
    }
}
