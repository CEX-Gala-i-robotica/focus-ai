using focus_ai;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using focus_ai.Prediction.Models;
using System.IO;
namespace focus_ai.Prediction;

public sealed record PredictionEngineResolution(
    PredictionEngine Engine,
    TrainingReport? TrainingReport,
    bool LoadedTrainedArtifact);

public static class PredictionModelService
{
    private static readonly ConcurrentDictionary<string, CachedEngine> EngineCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> FailedFingerprintCache = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string SchemaVersion = "next-session-v2";

    public static async Task<PredictionEngineResolution> GetEngineAsync(
        IReadOnlyList<SessionFeatures> history,
        string modelScope,
        CancellationToken cancellationToken = default)
    {
        if (history is null)
            throw new ArgumentNullException(nameof(history));
        if (string.IsNullOrWhiteSpace(modelScope))
            throw new ArgumentException("Identificatorul modelului nu poate fi gol.", nameof(modelScope));

        List<SessionFeatures> ordered = NextSessionFeatureBuilder.OrderAndValidate(history);
        if (ordered.Count < 2)
            return new PredictionEngineResolution(new PredictionEngine(), null, false);

        string fingerprint = NextSessionModelTrainer.CalculateHistoryFingerprint(ordered);
        string scopeKey = CreateScopeKey(modelScope);

        if (EngineCache.TryGetValue(scopeKey, out CachedEngine? cached) && cached.Fingerprint == fingerprint)
            return new PredictionEngineResolution(cached.Engine, cached.Report, cached.LoadedTrainedArtifact);

        SemaphoreSlim gate = Gates.GetOrAdd(scopeKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (EngineCache.TryGetValue(scopeKey, out cached) && cached.Fingerprint == fingerprint)
                return new PredictionEngineResolution(cached.Engine, cached.Report, cached.LoadedTrainedArtifact);

            string modelPath = GetModelPath(scopeKey);
            if (HasCurrentArtifact(modelPath, fingerprint))
            {
                try
                {
                    var engine = new PredictionEngine(modelPath);
                    bool loadedMl = engineUsesMlModel(modelPath);
                    var resolution = new PredictionEngineResolution(engine, null, loadedMl);
                    EngineCache[scopeKey] = new CachedEngine(fingerprint, engine, null, loadedMl);
                    return resolution;
                }
                catch
                {
                    RemoveArtifacts(modelPath);
                }
            }

            if (FailedFingerprintCache.TryGetValue(scopeKey, out string? failedFingerprint) && failedFingerprint == fingerprint)
            {
                var fallback = new PredictionEngine();
                EngineCache[scopeKey] = new CachedEngine(fingerprint, fallback, null, false);
                return new PredictionEngineResolution(fallback, null, false);
            }

            TrainingReport? report = null;
            try
            {
                report = await Task.Run(() => NextSessionModelTrainer.TrainAndSave(ordered, modelPath), cancellationToken)
                    .ConfigureAwait(false);
                if (report.Success && HasCurrentArtifact(modelPath, fingerprint))
                {
                    var trainedEngine = new PredictionEngine(modelPath);
                    bool loadedMl = engineUsesMlModel(modelPath);
                    EngineCache[scopeKey] = new CachedEngine(fingerprint, trainedEngine, report, loadedMl);
                    FailedFingerprintCache.TryRemove(scopeKey, out _);
                    return new PredictionEngineResolution(trainedEngine, report, loadedMl);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                report = new TrainingReport
                {
                    Success = false,
                    Message = "Antrenarea nu a putut fi finalizată. Se folosește un model statistic de bază."
                };
            }

            FailedFingerprintCache[scopeKey] = fingerprint;
            var baselineEngine = new PredictionEngine();
            EngineCache[scopeKey] = new CachedEngine(fingerprint, baselineEngine, report, false);
            return new PredictionEngineResolution(baselineEngine, report, false);
        }
        finally
        {
            gate.Release();
        }
    }

    public static string GetModelDirectory()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            throw new InvalidOperationException("Directorul local al aplicației nu este disponibil.");

        string directory = Path.Combine(localData, "FocusAI", "Models");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetModelPath(string scopeKey) => Path.Combine(GetModelDirectory(), scopeKey + ".zip");

    private static string CreateScopeKey(string modelScope)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(modelScope.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool HasCurrentArtifact(string modelPath, string fingerprint)
    {
        string metadataPath = modelPath + ".meta.json";
        if (!File.Exists(metadataPath))
            return false;

        try
        {
            PredictionModelMetadata? metadata = JsonSerializer.Deserialize<PredictionModelMetadata>(File.ReadAllText(metadataPath), JsonOptions);
            if (metadata is null || metadata.ArtifactVersion != 2 || metadata.FeatureSchemaVersion != SchemaVersion ||
                metadata.ScoreFormulaVersion != TestScoreCalculator.CurrentVersion ||
                !string.Equals(metadata.DataFingerprint, fingerprint, StringComparison.Ordinal) ||
                !double.IsFinite(metadata.ValidationMae) || !double.IsFinite(metadata.TestMae) || metadata.TestExamples <= 0)
                return false;

            if (NextSessionModelTrainer.IsMlModel(metadata.SelectedModel) && !File.Exists(modelPath))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool engineUsesMlModel(string modelPath)
    {
        string metadataPath = modelPath + ".meta.json";
        PredictionModelMetadata? metadata = JsonSerializer.Deserialize<PredictionModelMetadata>(File.ReadAllText(metadataPath), JsonOptions);
        return metadata is not null && NextSessionModelTrainer.IsMlModel(metadata.SelectedModel);
    }

    private static void RemoveArtifacts(string modelPath)
    {
        foreach (string path in new[] { modelPath, modelPath + ".meta.json", modelPath + ".tmp", modelPath + ".meta.json.tmp" })
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }

    private sealed record CachedEngine(
        string Fingerprint,
        PredictionEngine Engine,
        TrainingReport? Report,
        bool LoadedTrainedArtifact);
}
