using System.Collections.Generic;
using System.Linq;

namespace focus_ai;

/// <summary>
/// Parameters of X = Normal(mu, sigma) + Exponential(tau), in milliseconds.
/// IsAvailable is deliberately separate from the numeric fields so a missing fit is
/// never confused with a zero-millisecond distribution parameter.
/// </summary>
public sealed record ExGaussianFitResult
{
    public bool IsAvailable { get; init; }
    public bool IsReliable { get; init; }
    public int SampleCount { get; init; }
    public double MuMs { get; init; }
    public double SigmaMs { get; init; }
    public double TauMs { get; init; }
    public double LogLikelihood { get; init; }
    public bool Converged { get; init; }
    public int Iterations { get; init; }

    public static ExGaussianFitResult Unavailable(int sampleCount) => new()
    {
        IsAvailable = false,
        IsReliable = false,
        SampleCount = sampleCount,
        MuMs = 0,
        SigmaMs = 0,
        TauMs = 0,
        LogLikelihood = 0,
        Converged = false,
        Iterations = 0
    };
}

/// <summary>
/// Fits the ex-Gaussian reaction-time model by maximum likelihood.  The EM updates
/// use the latent exponential delay, and all likelihood calculations stay in log
/// space to remain stable in the slow-response tail.
/// </summary>
public static class ExGaussianFitter
{
    private const int MinimumSamples = 6;
    private const int MaximumIterations = 1_500;
    private const double LogSqrtTwoPi = 0.91893853320467274178;

    public static ExGaussianFitResult Fit(IEnumerable<double>? reactionTimesMs)
    {
        if (reactionTimesMs is null)
            return ExGaussianFitResult.Unavailable(0);

        double[] samples = reactionTimesMs
            .Where(value => double.IsFinite(value) && value > 0)
            .OrderBy(value => value)
            .ToArray();

        if (samples.Length < MinimumSamples)
            return ExGaussianFitResult.Unavailable(samples.Length);

        double mean = samples.Average();
        double variance = samples.Select(value => Square(value - mean)).Average();
        double standardDeviation = Math.Sqrt(Math.Max(variance, 0));
        double range = samples[^1] - samples[0];
        // Timings are currently recorded in integer milliseconds.  A five-ms floor
        // avoids the known small-sample likelihood degeneracy at sigma -> 0.
        double minimumScale = Math.Max(5.0, standardDeviation * 0.02);

        // A zero-variance response sequence is not identifiable as an ex-Gaussian.
        if (!double.IsFinite(standardDeviation) || standardDeviation <= minimumScale || range <= minimumScale)
            return ExGaussianFitResult.Unavailable(samples.Length);

        double thirdCentralMoment = samples.Select(value => Math.Pow(value - mean, 3)).Average();
        double skewTau = thirdCentralMoment > 0
            ? Math.Pow(thirdCentralMoment / 2.0, 1.0 / 3.0)
            : standardDeviation * 0.35;

        // EM can have local optima for short CPT series.  A small deterministic
        // multi-start set makes the maximum selected below substantially less
        // sensitive to the moment-based starting point.
        double[] starts =
        {
            standardDeviation * 0.10,
            standardDeviation * 0.25,
            standardDeviation * 0.50,
            standardDeviation * 0.75,
            standardDeviation * 0.90,
            skewTau
        };

        Candidate? best = null;
        foreach (double candidateTau in starts)
        {
            double tau = Math.Max(minimumScale, Math.Min(candidateTau, Math.Max(range * 4.0, minimumScale)));
            double sigma = Math.Max(minimumScale, Math.Sqrt(Math.Max(variance - tau * tau, minimumScale * minimumScale)));
            Candidate fitted = RunEm(samples, mean - tau, sigma, tau, minimumScale);
            if (fitted.IsFinite && (best is null || fitted.LogLikelihood > best.LogLikelihood))
                best = fitted;
        }

        if (best is null || !best.IsFinite)
            return ExGaussianFitResult.Unavailable(samples.Length);

        return new ExGaussianFitResult
        {
            IsAvailable = true,
            // CPT currently has about ten target trials.  Eight successful hits is
            // the minimum reporting threshold; consumers should still use the count.
            IsReliable = best.Converged && samples.Length >= 8,
            SampleCount = samples.Length,
            MuMs = RoundFinite(best.Mu),
            SigmaMs = RoundFinite(best.Sigma),
            TauMs = RoundFinite(best.Tau),
            LogLikelihood = RoundFinite(best.LogLikelihood),
            Converged = best.Converged,
            Iterations = best.Iterations
        };
    }

    private static Candidate RunEm(
        IReadOnlyList<double> values,
        double mu,
        double sigma,
        double tau,
        double minimumScale)
    {
        double[] expectedDelay = new double[values.Count];
        double[] expectedDelaySquared = new double[values.Count];
        double currentLogLikelihood = LogLikelihood(values, mu, sigma, tau);
        if (!double.IsFinite(currentLogLikelihood))
            return Candidate.Invalid;

        for (int iteration = 1; iteration <= MaximumIterations; iteration++)
        {
            double sumDelay = 0;
            for (int index = 0; index < values.Count; index++)
            {
                // Z | X=x is a N(a, sigma^2) distribution truncated at Z > 0.
                double a = values[index] - mu - sigma * sigma / tau;
                double alpha = a / sigma;
                double inverseMills = InverseMillsRatio(alpha);
                double delay = a + sigma * inverseMills;
                double delaySquared = a * a + sigma * sigma + a * sigma * inverseMills;

                if (!double.IsFinite(delay) || !double.IsFinite(delaySquared) || delay <= 0)
                    return Candidate.Invalid;

                expectedDelay[index] = delay;
                expectedDelaySquared[index] = Math.Max(delaySquared, delay * delay);
                sumDelay += delay;
            }

            double newTau = Math.Max(sumDelay / values.Count, minimumScale);
            double newMu = values.Zip(expectedDelay, (value, delay) => value - delay).Average();
            double sigmaSquared = 0;
            for (int index = 0; index < values.Count; index++)
            {
                double residual = values[index] - newMu;
                sigmaSquared += residual * residual - 2 * residual * expectedDelay[index] + expectedDelaySquared[index];
            }

            double newSigma = Math.Sqrt(Math.Max(sigmaSquared / values.Count, minimumScale * minimumScale));
            double newLogLikelihood = LogLikelihood(values, newMu, newSigma, newTau);
            if (!double.IsFinite(newLogLikelihood))
                return Candidate.Invalid;

            // Numerical round-off may make a theoretically monotonic EM step very
            // slightly worse.  Retain the last valid point in that rare situation.
            if (newLogLikelihood + 1e-8 < currentLogLikelihood)
                return new Candidate(mu, sigma, tau, currentLogLikelihood, false, iteration - 1);

            double parameterChange = Math.Max(
                Math.Abs(newMu - mu) / Math.Max(1.0, Math.Abs(mu)),
                Math.Max(
                    Math.Abs(newSigma - sigma) / Math.Max(1.0, sigma),
                    Math.Abs(newTau - tau) / Math.Max(1.0, tau)));
            double likelihoodChange = Math.Abs(newLogLikelihood - currentLogLikelihood);

            mu = newMu;
            sigma = newSigma;
            tau = newTau;
            currentLogLikelihood = newLogLikelihood;

            if (parameterChange < 1e-6 || likelihoodChange < 1e-7)
                return new Candidate(mu, sigma, tau, currentLogLikelihood, true, iteration);
        }

        return new Candidate(mu, sigma, tau, currentLogLikelihood, false, MaximumIterations);
    }

    private static double LogLikelihood(IReadOnlyList<double> values, double mu, double sigma, double tau)
    {
        if (!double.IsFinite(mu) || !double.IsFinite(sigma) || !double.IsFinite(tau) || sigma <= 0 || tau <= 0)
            return double.NegativeInfinity;

        double sigmaOverTau = sigma / tau;
        double quadratic = sigmaOverTau * sigmaOverTau / 2.0;
        double logLikelihood = 0;
        foreach (double value in values)
        {
            double z = (value - mu) / sigma - sigmaOverTau;
            double logDensity = -Math.Log(tau) + (mu - value) / tau + quadratic + LogStandardNormalCdf(z);
            if (!double.IsFinite(logDensity))
                return double.NegativeInfinity;
            logLikelihood += logDensity;
        }

        return logLikelihood;
    }

    // phi(x) / Phi(x), calculated without underflow when x is deep in the left tail.
    private static double InverseMillsRatio(double x)
    {
        if (x < -8)
        {
            double t = -x;
            return t + 1.0 / t - 2.0 / (t * t * t);
        }

        double logRatio = -0.5 * x * x - LogSqrtTwoPi - LogStandardNormalCdf(x);
        return logRatio > 700 ? double.PositiveInfinity : Math.Exp(logRatio);
    }

    private static double LogStandardNormalCdf(double x)
    {
        if (x < -5)
        {
            // Mills-ratio expansion of log(Phi(x)); accurate and stable in the tail.
            double t = -x;
            double inverseSquare = 1.0 / (t * t);
            double correction = 1.0 - inverseSquare + 3.0 * inverseSquare * inverseSquare
                - 15.0 * Math.Pow(inverseSquare, 3) + 105.0 * Math.Pow(inverseSquare, 4);
            return -0.5 * t * t - Math.Log(t) - LogSqrtTwoPi + Math.Log(Math.Max(correction, 1e-12));
        }

        if (x > 5)
        {
            double logTail = LogStandardNormalCdf(-x);
            return -Math.Exp(logTail);
        }

        return Math.Log(Math.Max(StandardNormalCdf(x), 1e-300));
    }

    // Abramowitz-Stegun approximation; the tail is handled separately above.
    private static double StandardNormalCdf(double x)
    {
        double absolute = Math.Abs(x);
        double t = 1.0 / (1.0 + 0.2316419 * absolute);
        double polynomial = t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + 1.330274429 * t))));
        double upper = polynomial * Math.Exp(-0.5 * absolute * absolute) / Math.Sqrt(2.0 * Math.PI);
        double cdf = absolute >= 0 ? 1.0 - upper : upper;
        return x >= 0 ? cdf : 1.0 - cdf;
    }

    private static double Square(double value) => value * value;

    private static double RoundFinite(double value) =>
        double.IsFinite(value) ? Math.Round(value, 6) : 0;

    private sealed record Candidate(double Mu, double Sigma, double Tau, double LogLikelihood, bool Converged, int Iterations)
    {
        public bool IsFinite => double.IsFinite(Mu) && double.IsFinite(Sigma) && double.IsFinite(Tau) &&
                                double.IsFinite(LogLikelihood) && Sigma > 0 && Tau > 0;

        public static Candidate Invalid { get; } = new(0, 0, 0, double.NegativeInfinity, false, 0);
    }
}
