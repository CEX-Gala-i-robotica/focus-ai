using System.Text.Json;
using focus_ai.Prediction;
using Xunit;

namespace FocusAI.Tests;

public sealed class FeatureExtractorTests
{
    [Fact]
    public void FromJsonElement_ExtractsTauHrvAndKalmanFeatures()
    {
        const string json = """
        {
          "scor": 72.5,
          "scoreVersion": 3,
          "dateTime": "24.11.2026 10:30",
          "duration": "03:00",
          "map": "10,10;20,20;30,30;40,40;50,50",
          "tr2": 0.41,
          "precizie_gonogo": 88,
          "rr_ms": "760,810,780,840,800,770,830,790,815,785,845,805,775,835,795,820,790,810,780,840,800,770,830,790,815,785",
          "rr_source": "MAX30102_PPG",
          "rr_measurement_type": "PRV",
          "cpt": {
            "accuracy": 90,
            "hit_rate": 0.9,
            "false_alarm_rate": 0.08,
            "mu_ms": 420,
            "sigma_ms": 45,
            "tau_ms": 135,
            "reaction_time_cv": 0.22,
            "reaction_time_sample_count": 12
          },
          "attention": {
            "estimate": 0.73,
            "posterior_standard_deviation": 0.08,
            "confidence_low": 0.57,
            "confidence_high": 0.89,
            "observation_count": 5
          }
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        var features = FeatureExtractor.FromJsonElement(doc.RootElement);

        Assert.True(features.HasScoreData);
        Assert.Equal(72.5, features.Score, precision: 6);
        Assert.True(features.HasCptExGaussianData);
        Assert.Equal(135, features.CptTauMs, precision: 6);
        Assert.True(features.HasHrvData);
        Assert.True(features.RrSampleCount >= 20);
        Assert.True(features.HasAttentionData);
        Assert.Equal(0.73, features.AttentionEstimate, precision: 6);
    }

    [Fact]
    public void FromJsonElement_PreservesValidLegacyScoreInsteadOfSilentlyMigratingIt()
    {
        const string json = """
        {
          "scor": 41.25,
          "scoreVersion": 2,
          "map": "10,10;20,20;30,30;40,40;50,50",
          "tr2": 0.30,
          "precizie_gonogo": 98,
          "cpt": { "accuracy": 96 }
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        var features = FeatureExtractor.FromJsonElement(doc.RootElement);

        Assert.True(features.HasScoreData);
        Assert.Equal(41.25, features.Score, precision: 6);
        Assert.Equal(2, features.ScoreFormulaVersion);
    }
    [Fact]
    public void FromJsonElement_DoesNotTreatUnlabelledIntervalsAsHrvFeatures()
    {
        const string json = """
        {
          "scor": 70,
          "rr_ms": "760,810,780,840,800,770,830,790,815,785,845,805,775,835,795,820,790,810,780,840,800,770,830,790,815,785"
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        var features = FeatureExtractor.FromJsonElement(doc.RootElement);

        Assert.False(features.HasHrvData);
        Assert.Equal(0, features.RrSampleCount);
    }

    [Fact]
    public void FromJsonElement_DoesNotTreatMyoWareRawAnalogSamplesAsValidatedEcg()
    {
        const string json = """
        {
          "scor": 70,
          "analog_signal": "100,120;110,130;120,140",
          "analog_signal_source": "MyoWare analog output A0 (raw ADC)",
          "analog_signal_is_clinically_validated_ecg": false
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        var features = FeatureExtractor.FromJsonElement(doc.RootElement);

        Assert.False(features.HasEcgData);
        Assert.Equal(0, features.EcgSampleCount);
    }

    [Fact]
    public void FromJsonElement_ExcludesUnreliableExGaussianFitFromMlFeatures()
    {
        const string json = """
        {
          "scor": 70,
          "cpt": {
            "mu_ms": 410,
            "sigma_ms": 55,
            "tau_ms": 190,
            "reaction_time_cv": 0.3,
            "reaction_time_sample_count": 14,
            "ex_gaussian_available": true,
            "ex_gaussian_converged": false,
            "ex_gaussian_reliable": false
          }
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        var features = FeatureExtractor.FromJsonElement(doc.RootElement);

        Assert.False(features.HasCptExGaussianData);
        Assert.Equal(0, features.CptTauMs);
    }

}
