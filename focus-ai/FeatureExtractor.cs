using System.Globalization;
using System.Text.Json;
using focus_ai.Prediction.Models;

namespace focus_ai.Prediction;


public static class FeatureExtractor
{
    public static SessionFeatures FromJsonElement(JsonElement el)
    {
        bool hasScore = TryGetDouble(el, out double score, "scor", "score");
        bool hasTr2 = TryGetDouble(el, out double tr2, "tr2");
        bool hasGoNoGo = TryGetDouble(el, out double goNoGo, "precizieGonogo", "precizie_gonogo");

        string durationText = GetString(el, "duration");
        string dateTimeText = GetString(el, "dateTime", "datetime", "timestamp");

        double[] hr = ParseCsvDoubles(GetString(el, "hr"))
            .Where(v => v >= 30 && v <= 220)
            .ToArray();
        var (hrMean, hrStd, hrMin, hrMax) = MeanStdMinMax(hr);

        // Keep plausible numeric SpO2 values; zero and values above 100 are treated as invalid.
        double[] spo2 = ParseCsvDoubles(GetString(el, "spo2"))
            .Where(v => v > 0 && v <= 100)
            .ToArray();
        double spo2Mean = spo2.Length > 0 ? spo2.Average() : 0;
        double spo2Min = spo2.Length > 0 ? spo2.Min() : 0;
        double spo2Drops = spo2.Count(v => v < 95);

        double[] dist = ParseCsvDoubles(GetString(el, "dist"));
        double distRatio = dist.Length > 0
            ? dist.Count(v => v > 0.5) / (double)dist.Length
            : 0;

        var (ecgDrMean, ecgStMean, ecgRange, ecgCount) = EcgStats(GetString(el, "ecg"));
        var (mapXVar, mapYVar, mapPath, mapCount) = MapStats(GetString(el, "map"));

        return new SessionFeatures
        {
            Score = hasScore ? score : 0,
            HasScoreData = hasScore && score >= 0 && score <= 100,
            Tr2 = hasTr2 ? tr2 : 0,
            HasTr2Data = hasTr2,
            PrecizieGonogo = hasGoNoGo ? goNoGo : 0,
            HasPrecizieGonogoData = hasGoNoGo,

            HrMean = hrMean,
            HrStd = hrStd,
            HrMin = hrMin,
            HrMax = hrMax,
            HrRange = hr.Length > 0 ? hrMax - hrMin : 0,
            HrSampleCount = hr.Length,
            HasHrData = hr.Length > 0,

            Spo2Mean = spo2Mean,
            Spo2Min = spo2Min,
            Spo2DropCount = spo2Drops,
            Spo2SampleCount = spo2.Length,
            HasSpo2Data = spo2.Length > 0,

            DistRatio = distRatio,
            DistSampleCount = dist.Length,
            HasDistData = dist.Length > 0,

            EcgDrMean = ecgDrMean,
            EcgStMean = ecgStMean,
            EcgRange = ecgRange,
            EcgSampleCount = ecgCount,
            HasEcgData = ecgCount > 0,

            MapXVar = mapXVar,
            MapYVar = mapYVar,
            MapTotalPath = mapPath,
            MapSampleCount = mapCount,
            HasMapData = mapCount > 0,

            DurationMinutes = ParseDuration(durationText),
            DateTime = ParseDateTime(dateTimeText)
        };
    }

    private static string GetString(JsonElement el, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement p))
                continue;

            switch (p.ValueKind)
            {
                case JsonValueKind.String:
                    return p.GetString() ?? "";
                case JsonValueKind.Number:
                    return p.GetRawText();
                case JsonValueKind.Array:
                    var values = new List<string>();
                    foreach (JsonElement item in p.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number)
                            values.Add(item.GetRawText());
                        else if (item.ValueKind == JsonValueKind.String &&
                                 !string.IsNullOrWhiteSpace(item.GetString()))
                            values.Add(item.GetString()!);
                    }
                    return string.Join(",", values);
            }
        }

        return "";
    }

    private static bool TryGetDouble(JsonElement el, out double value, params string[] names)
    {
        foreach (string name in names)
        {
            if (!el.TryGetProperty(name, out JsonElement p))
                continue;

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out value) && double.IsFinite(value))
                return true;

            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                double.IsFinite(value))
                return true;
        }

        value = 0;
        return false;
    }

    private static double[] ParseCsvDoubles(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<double>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
                ? (double?)v
                : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
    }

    private static (double mean, double std, double min, double max) MeanStdMinMax(double[] values)
    {
        if (values.Length == 0)
            return (0, 0, 0, 0);

        double mean = values.Average();
        double variance = values.Select(v => (v - mean) * (v - mean)).Average();
        return (mean, Math.Sqrt(variance), values.Min(), values.Max());
    }

    private static double ParseDuration(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        // Plain numeric values are interpreted as minutes.
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) &&
            double.IsFinite(minutes) && minutes >= 0)
            return minutes;

        string[] parts = s.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sec) &&
            m >= 0 && sec is >= 0 and < 60)
            return m + sec / 60.0;

        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mm) &&
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ss) &&
            h >= 0 && mm is >= 0 and < 60 && ss is >= 0 and < 60)
            return h * 60.0 + mm + ss / 60.0;

        return 0;
    }

    private static DateTime ParseDateTime(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return DateTime.MinValue;

        string[] formats =
        {
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd HH:mm:ss",
            "O"
        };

        if (DateTime.TryParseExact(
                s,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out DateTime exact))
            return exact;

        if (DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out DateTimeOffset offset))
            return offset.LocalDateTime;

        return DateTime.MinValue;
    }

    private static (double drMean, double stMean, double range, int count) EcgStats(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (0, 0, 0, 0);

        var drValues = new List<double>();
        var stValues = new List<double>();
        var allValues = new List<double>();
        int validPairs = 0;

        foreach (string pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pts = pair.Split(',', StringSplitOptions.TrimEntries);
            if (pts.Length < 2)
                continue;

            bool hasDr = double.TryParse(pts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double dr) && double.IsFinite(dr);
            bool hasSt = double.TryParse(pts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double st) && double.IsFinite(st);

            if (hasDr)
            {
                drValues.Add(dr);
                allValues.Add(dr);
            }
            if (hasSt)
            {
                stValues.Add(st);
                allValues.Add(st);
            }
            if (hasDr && hasSt)
                validPairs++;
        }

        if (allValues.Count == 0)
            return (0, 0, 0, 0);

        return (
            drValues.Count > 0 ? drValues.Average() : 0,
            stValues.Count > 0 ? stValues.Average() : 0,
            allValues.Max() - allValues.Min(),
            validPairs);
    }

    private static (double xVar, double yVar, double totalPath, int count) MapStats(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (0, 0, 0, 0);

        var xs = new List<double>();
        var ys = new List<double>();

        foreach (string pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pts = pair.Split(',', StringSplitOptions.TrimEntries);
            if (pts.Length < 2)
                continue;

            bool okX = double.TryParse(pts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.IsFinite(x);
            bool okY = double.TryParse(pts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) && double.IsFinite(y);
            // Preserve X/Y pairing: never append only one coordinate.
            if (okX && okY)
            {
                xs.Add(x);
                ys.Add(y);
            }
        }

        if (xs.Count == 0)
            return (0, 0, 0, 0);

        double xMean = xs.Average();
        double yMean = ys.Average();
        double xVar = xs.Select(v => (v - xMean) * (v - xMean)).Average();
        double yVar = ys.Select(v => (v - yMean) * (v - yMean)).Average();

        double path = 0;
        for (int i = 1; i < xs.Count; i++)
        {
            double dx = xs[i] - xs[i - 1];
            double dy = ys[i] - ys[i - 1];
            path += Math.Sqrt(dx * dx + dy * dy);
        }

        return (xVar, yVar, path, xs.Count);
    }
}
