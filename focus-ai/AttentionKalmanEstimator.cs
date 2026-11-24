using System.Collections.ObjectModel;

namespace focus_ai;

/// <summary>
/// One noisy, bounded observation of the latent attention state. Values are
/// normalised to [0, 1]; variance is the declared observation-model variance.
/// </summary>
public sealed record AttentionObservation(
    string Source,
    double Value,
    double Variance,
    string? Detail = null);

/// <summary>Audit information for one Kalman update.</summary>
public sealed record AttentionUpdate(
    string Source,
    double Observation,
    double ObservationVariance,
    double KalmanGain,
    double PosteriorEstimate,
    double PosteriorStandardDeviation,
    string? Detail);

/// <summary>
/// Posterior session-level attention state. The 95% interval is conditional on
/// the declared Gaussian observation models; it is not a diagnostic interval.
/// </summary>
public sealed record AttentionEstimate
{
    public double Estimate { get; init; }
    public double Score { get; init; }
    public double PosteriorVariance { get; init; }
    public double PosteriorStandardDeviation { get; init; }
    public double ConfidenceLow { get; init; }
    public double ConfidenceHigh { get; init; }
    public int ObservationCount { get; init; }
    public IReadOnlyList<AttentionUpdate> Updates { get; init; } = Array.Empty<AttentionUpdate>();
}

/// <summary>
/// Scalar Kalman filter for a continuous latent attention state.
///
/// x- = x, P- = P + Q; K = P- / (P- + R);
/// x = x- + K(z - x-), P = (1 - K)P-.
/// Missing sensors simply provide no update, leaving a wider posterior.
/// </summary>
public sealed class AttentionKalmanEstimator
{
    private const double PriorMean = 0.50;
    private const double PriorVariance = 0.25;
    private const double SessionProcessVariance = 0.01;
    private const double MinimumObservationVariance = 0.0025;
    private const double MaximumObservationVariance = 1.0;

    public AttentionEstimate Estimate(IEnumerable<AttentionObservation>? observations)
    {
        double state = PriorMean;
        double variance = PriorVariance + SessionProcessVariance;
        var updates = new List<AttentionUpdate>();

        if (observations is not null)
        {
            foreach (AttentionObservation observation in observations)
            {
                if (string.IsNullOrWhiteSpace(observation.Source) ||
                    !double.IsFinite(observation.Value) ||
                    !double.IsFinite(observation.Variance) ||
                    observation.Variance <= 0)
                {
                    continue;
                }

                double measurement = Math.Clamp(observation.Value, 0, 1);
                double measurementVariance = Math.Clamp(
                    observation.Variance,
                    MinimumObservationVariance,
                    MaximumObservationVariance);
                double gain = variance / (variance + measurementVariance);

                state = Math.Clamp(state + gain * (measurement - state), 0, 1);
                variance = Math.Max(MinimumObservationVariance * 0.1, (1 - gain) * variance);

                updates.Add(new AttentionUpdate(
                    observation.Source,
                    measurement,
                    measurementVariance,
                    gain,
                    state,
                    Math.Sqrt(variance),
                    observation.Detail));
            }
        }

        double standardDeviation = Math.Sqrt(variance);
        return new AttentionEstimate
        {
            Estimate = state,
            Score = state * 100,
            PosteriorVariance = variance,
            PosteriorStandardDeviation = standardDeviation,
            ConfidenceLow = Math.Clamp(state - 1.96 * standardDeviation, 0, 1),
            ConfidenceHigh = Math.Clamp(state + 1.96 * standardDeviation, 0, 1),
            ObservationCount = updates.Count,
            Updates = new ReadOnlyCollection<AttentionUpdate>(updates)
        };
    }
}
