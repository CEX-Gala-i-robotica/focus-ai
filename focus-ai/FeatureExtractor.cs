using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using focus_ai.Prediction.Models;

namespace focus_ai.Prediction
{
    public static class FeatureExtractor
    {
        public static SessionFeatures FromJsonElement(JsonElement el)
        {
            double score = GetDouble(el, "scor");
            double tr2 = GetDouble(el, "tr2");
            double precizieGonogo = GetDouble(el, "precizieGonogo", "precizie_gonogo");

            string durationStr = GetString(el, "duration");
            string dateTimeStr = GetString(el, "dateTime");

            double[] hr = ParseCsvDoubles(GetString(el, "hr"));
            var (hrMean, hrStd, hrMin, hrMax) = HrStats(hr);

            double[] spo2 = ParseCsvDoubles(GetString(el, "spo2"));
            double spo2Mean = spo2.Length > 0 ? spo2.Average() : 0;
            double spo2Min = spo2.Length > 0 ? spo2.Min() : 0;
            double spo2Drops = spo2.Count(v => v < 95);

            double[] dist = ParseCsvDoubles(GetString(el, "dist"));
            double distRatio = dist.Length > 0 ? dist.Count(v => v > 0.5) / (double)dist.Length : 0;

            var (ecgDrMean, ecgStMean, ecgRange) = EcgStats(GetString(el, "ecg"));
            var (mapXVar, mapYVar, mapPath) = MapStats(GetString(el, "map"));

            return new SessionFeatures
            {
                Score = score,
                Tr2 = tr2,
                PrecizieGonogo = precizieGonogo,
                HrMean = hrMean,
                HrStd = hrStd,
                HrMin = hrMin,
                HrMax = hrMax,
                HrRange = hrMax - hrMin,
                Spo2Mean = spo2Mean,
                Spo2Min = spo2Min,
                Spo2DropCount = spo2Drops,
                DistRatio = distRatio,
                EcgDrMean = ecgDrMean,
                EcgStMean = ecgStMean,
                EcgRange = ecgRange,
                MapXVar = mapXVar,
                MapYVar = mapYVar,
                MapTotalPath = mapPath,
                DurationMinutes = ParseDuration(durationStr),
                DateTime = ParseDateTime(dateTimeStr)
            };
        }

        private static string GetString(JsonElement el, string name)
            => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? ""
                : "";

        private static double GetDouble(JsonElement el, params string[] names)
        {
            foreach (string name in names)
            {
                if (!el.TryGetProperty(name, out var p)) continue;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out double n)) return n;
                if (p.ValueKind == JsonValueKind.String &&
                    double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                    return s;
            }
            return 0;
        }

        private static double[] ParseCsvDoubles(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<double>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0)
                .ToArray();
        }

        private static double ParseDuration(string s)
        {
            var parts = s.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int sec))
                return m + sec / 60.0;
            return 0;
        }

        private static DateTime ParseDateTime(string s)
        {
            string[] formats =
            {
                "dd.MM.yyyy HH:mm",
                "dd.MM.yyyy HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ",
                "O"
            };

            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out DateTime exact))
                return exact;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime any))
                return any;
            return DateTime.MinValue;
        }

        private static (double mean, double std, double min, double max) HrStats(double[] hr)
        {
            var valid = hr.Where(v => v >= 30 && v <= 220).ToArray();
            if (valid.Length == 0) return (0, 0, 0, 0);

            double mean = valid.Average();
            double std = Math.Sqrt(valid.Select(v => (v - mean) * (v - mean)).Average());
            return (mean, std, valid.Min(), valid.Max());
        }

        private static (double drMean, double stMean, double range) EcgStats(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (0, 0, 0);

            var drList = new List<double>();
            var stList = new List<double>();
            foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pts = pair.Split(',');
                if (pts.Length < 2) continue;
                if (double.TryParse(pts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double dr))
                    drList.Add(dr);
                if (double.TryParse(pts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double st))
                    stList.Add(st);
            }

            if (drList.Count == 0) return (0, 0, 0);
            double allMin = Math.Min(drList.Min(), stList.Count > 0 ? stList.Min() : drList.Min());
            double allMax = Math.Max(drList.Max(), stList.Count > 0 ? stList.Max() : drList.Max());
            return (drList.Average(), stList.Count > 0 ? stList.Average() : 0, allMax - allMin);
        }

        private static (double xVar, double yVar, double totalPath) MapStats(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (0, 0, 0);

            var xs = new List<double>();
            var ys = new List<double>();
            foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pts = pair.Split(',');
                if (pts.Length < 2) continue;
                if (double.TryParse(pts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                    xs.Add(x);
                if (double.TryParse(pts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                    ys.Add(y);
            }

            if (xs.Count < 2 || ys.Count < 2) return (0, 0, 0);
            double xMean = xs.Average();
            double yMean = ys.Average();
            double xVar = xs.Select(v => (v - xMean) * (v - xMean)).Average();
            double yVar = ys.Select(v => (v - yMean) * (v - yMean)).Average();

            double path = 0;
            for (int i = 1; i < Math.Min(xs.Count, ys.Count); i++)
            {
                double dx = xs[i] - xs[i - 1];
                double dy = ys[i] - ys[i - 1];
                path += Math.Sqrt(dx * dx + dy * dy);
            }

            return (xVar, yVar, path);
        }
    }
}
