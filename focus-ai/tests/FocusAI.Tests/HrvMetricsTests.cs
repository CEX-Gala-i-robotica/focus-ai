using focus_ai;
using Xunit;

namespace FocusAI.Tests;

public sealed class HrvMetricsTests
{
    [Fact]
    public void Analyze_CalculatesSampleSdnnAndRmssdFromRrIntervals()
    {
        HrvMetricsResult result = HrvMetrics.Analyze(new[] { 800d, 900d, 1000d, 1100d });

        Assert.True(result.HasTimeDomainMetrics);
        Assert.Equal(4, result.RrSampleCount);
        Assert.Equal(129.099, result.SdnnMs!.Value, precision: 3);
        Assert.Equal(100, result.RmssdMs!.Value, precision: 6);
    }

    [Fact]
    public void Analyze_FiltersPhysiologicallyInvalidIntervalsWithoutInventingRr()
    {
        HrvMetricsResult result = HrvMetrics.Analyze(new[]
        {
            200d, double.NaN, 800d, 900d, 2_500d
        });

        Assert.Equal(2, result.RrSampleCount);
        Assert.True(result.HasTimeDomainMetrics);
        Assert.Equal(70.710678, result.SdnnMs!.Value, precision: 5);
        Assert.Equal(100, result.RmssdMs!.Value, precision: 6);
    }

    [Fact]
    public void Analyze_LeavesComplexityUnavailableForConstantOrTooShortSeries()
    {
        HrvMetricsResult constant = HrvMetrics.Analyze(Enumerable.Repeat(800d, 30));
        HrvMetricsResult shortSeries = HrvMetrics.Analyze(new[] { 800d });

        Assert.True(constant.HasTimeDomainMetrics);
        Assert.Equal(0, constant.SdnnMs);
        Assert.Equal(0, constant.RmssdMs);
        Assert.False(constant.HasDfaAlpha1);
        Assert.False(constant.HasSampleEntropy);
        Assert.False(shortSeries.IsAvailable);
    }

    [Fact]
    public void Analyze_ProvidesNonlinearMeasuresForVariableRrSeries()
    {
        double[] rr = Enumerable.Range(0, 96)
            .Select(index => 820d + 45 * Math.Sin(index * 0.43) + 18 * Math.Sin(index * 1.71) + (index % 7 - 3) * 6)
            .ToArray();

        HrvMetricsResult result = HrvMetrics.Analyze(rr);

        Assert.True(result.HasDfaAlpha1);
        Assert.True(result.HasSampleEntropy);
        Assert.True(result.DfaAlpha1 is > 0 and < 3);
        Assert.True(result.SampleEntropy is > 0 and < 4);
    }
}
