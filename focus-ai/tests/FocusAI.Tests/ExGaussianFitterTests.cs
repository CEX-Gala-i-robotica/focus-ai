using focus_ai;
using Xunit;

namespace FocusAI.Tests;

public sealed class ExGaussianFitterTests
{
    [Fact]
    public void Fit_ReturnsUnavailableForTooFewValidReactionTimes()
    {
        ExGaussianFitResult result = ExGaussianFitter.Fit(new[]
        {
            double.NaN, -20d, 0d, 410d, 430d, 470d, 510d, 550d
        });

        Assert.False(result.IsAvailable);
        Assert.False(result.IsReliable);
        Assert.Equal(5, result.SampleCount);
        Assert.Equal(0, result.TauMs);
    }

    [Fact]
    public void Fit_RecoversSlowTailFromDeterministicExGaussianSample()
    {
        double[] reactionTimes = GenerateExGaussian(
            count: 320,
            mu: 430,
            sigma: 42,
            tau: 115,
            seed: 8841);

        ExGaussianFitResult result = ExGaussianFitter.Fit(reactionTimes);

        Assert.True(result.IsAvailable);
        Assert.True(result.Converged);
        Assert.True(result.IsReliable);
        Assert.InRange(result.TauMs, 55, 185);
        Assert.InRange(result.MuMs, 350, 510);
        Assert.True(double.IsFinite(result.LogLikelihood));
    }

    [Fact]
    public void Fit_DetectsLargerTailForSlowTailData()
    {
        ExGaussianFitResult compact = ExGaussianFitter.Fit(GenerateExGaussian(280, 460, 45, 30, 92));
        ExGaussianFitResult slowTail = ExGaussianFitter.Fit(GenerateExGaussian(280, 460, 45, 145, 92));

        Assert.True(compact.IsAvailable);
        Assert.True(slowTail.IsAvailable);
        Assert.True(slowTail.TauMs > compact.TauMs + 30,
            $"Expected slow tail τ ({slowTail.TauMs:F1}) to exceed compact τ ({compact.TauMs:F1}).");
    }

    private static double[] GenerateExGaussian(int count, double mu, double sigma, double tau, int seed)
    {
        var random = new Random(seed);
        var values = new double[count];
        for (int index = 0; index < count; index++)
        {
            double u1 = 1 - random.NextDouble();
            double u2 = 1 - random.NextDouble();
            double gaussian = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            double exponential = -tau * Math.Log(1 - random.NextDouble());
            values[index] = mu + sigma * gaussian + exponential;
        }

        return values;
    }
}
