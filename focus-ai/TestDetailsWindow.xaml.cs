using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace focus_ai
{
    public partial class TestDetailsWindow : Window
    {
        private readonly string _mapRaw;
        private readonly string _ecgRaw;
        private readonly string _spo2Raw;
        private readonly string _hrRaw;
        private readonly string _distRaw;
        private readonly string _cptRaw;
        private readonly string _rrRaw;
        private readonly string _attentionRaw;
        
        public TestDetailsWindow(string mapRaw, string ecgRaw,
                                  string spo2Raw, string hrRaw, string distRaw,
                                  string cptRaw = "", string rrRaw = "", string attentionRaw = "")
        {
            InitializeComponent();
            _mapRaw = mapRaw;
            _ecgRaw = ecgRaw;
            _spo2Raw = spo2Raw;
            _hrRaw = hrRaw;
            _distRaw = distRaw;
            _cptRaw = cptRaw;
            _rrRaw = rrRaw;
            _attentionRaw = attentionRaw;
            LanguageManager.Register(this);
            WindowHelper.MoveToPrimaryMonitor(this);
            Loaded += (_, _) => DrawAll(_mapRaw, _ecgRaw, _spo2Raw, _hrRaw, _distRaw, _cptRaw, _rrRaw, _attentionRaw);
            LanguageManager.LanguageChanged += TestDetailsWindow_LanguageChanged;
            Closed += (_, _) => LanguageManager.LanguageChanged -= TestDetailsWindow_LanguageChanged;
        }

        private void TestDetailsWindow_LanguageChanged(object? sender, EventArgs e)
        {
            if (!IsLoaded) return;

            ClearChartVisuals();
            DrawAll(_mapRaw, _ecgRaw, _spo2Raw, _hrRaw, _distRaw, _cptRaw, _rrRaw, _attentionRaw);
        }

        private void ClearChartVisuals()
        {
            MapCanvas.Children.Clear();
            MapXLabels.Children.Clear();
            MapYLabels.Children.Clear();
            EcgCanvas1.Children.Clear();
            EcgXLabels.Children.Clear();
            EcgYLabels.Children.Clear();
            Spo2Canvas.Children.Clear();
            Spo2XLabels.Children.Clear();
            Spo2YLabels.Children.Clear();
            HrCanvas.Children.Clear();
            HrXLabels.Children.Clear();
            HrYLabels.Children.Clear();
            DistCanvas.Children.Clear();
        }

        
        private void DrawAll(string mapRaw, string ecgRaw,
                              string spo2Raw, string hrRaw, string distRaw,
                              string cptRaw, string rrRaw, string attentionRaw)
        {
            
            var mapPoints = ParseXY(mapRaw);
            if (mapPoints.Count > 0)
                DrawScatter(MapCanvas, MapXLabels, MapYLabels, mapPoints);

            
            var ecgPairs = ParsePairs(ecgRaw);
            if (ecgPairs.Count > 0)
            {
                var ch1 = ecgPairs.Select((p, i) => new Point(i, p.X)).ToList();
                var ch2 = ecgPairs.Select((p, i) => new Point(i, p.Y)).ToList();

                double minY = Math.Min(ch1.Min(p => p.Y), ch2.Min(p => p.Y));
                double maxY = Math.Max(ch1.Max(p => p.Y), ch2.Max(p => p.Y));
                double minX = 0;
                double maxX = ecgPairs.Count - 1;

                DrawLineChart(EcgCanvas1, EcgXLabels, EcgYLabels,
                    ch1, GradH(Color.FromRgb(201, 196, 255), Color.FromRgb(237, 237, 237)),
                    minX, maxX, minY, maxY);
                DrawLineChart(EcgCanvas1, null, null,
                    ch2, GradH(Color.FromRgb(194, 194, 194), Color.FromRgb(201, 196, 255)),
                    minX, maxX, minY, maxY, skipGrid: true);
            }

            
            var spo2 = ParseValues(spo2Raw);
            if (spo2.Count > 0)
                DrawLineChart(Spo2Canvas, Spo2XLabels, Spo2YLabels,
                    Indexed(spo2),
                    GradH(Color.FromRgb(201, 196, 255), Color.FromRgb(201, 196, 255)));

            
            var hr = ParseValues(hrRaw);
            if (hr.Count > 0)
                DrawLineChart(HrCanvas, HrXLabels, HrYLabels,
                    Indexed(hr),
                    GradH(Color.FromRgb(201, 196, 255), Color.FromRgb(194, 194, 194)));

            
            var dist = ParseValues(distRaw);
            if (dist.Count > 0)
                DrawDistAxis(DistCanvas, dist);

            
            UpdateStats(mapPoints, ecgPairs, spo2, hr, dist, cptRaw);
            UpdateScientificMetrics(cptRaw, rrRaw, attentionRaw);
        }

        private void UpdateStats(
            List<Point> mapPoints,
            List<Point> ecgPairs,
            List<double> spo2,
            List<double> hr,
            List<double> dist,
            string cptRaw)
        {
            double spo2Min = spo2.Any(v => v > 0) ? spo2.Where(v => v > 0).Min() : 0;
            double spo2Max = spo2.Any() ? spo2.Max() : 0;
            double hrMin = hr.Any(v => v > 0) ? hr.Where(v => v > 0).Min() : 0;
            double hrMax = hr.Any() ? hr.Max() : 0;
            int distActive = dist.Count(v => v > 0);

            string pointLabel = LanguageManager.Current == AppLanguage.Romanian ? "puncte" : "points";
            string activeLabel = LanguageManager.Current == AppLanguage.Romanian ? "active" : "active";

            StatsText.Text =
                $"  MAP {mapPoints.Count} {pointLabel}   ·   " +
                $"ECG {ecgPairs.Count} {pointLabel}   ·   " +
                $"SPO2 [{FormatNumber(spo2Min, "F0")}–{FormatNumber(spo2Max, "F0")}]%   ·   " +
                $"HR [{FormatNumber(hrMin, "F0")}–{FormatNumber(hrMax, "F0")}] bpm   ·   " +
                $"DIST {distActive}/{dist.Count} {activeLabel}" +
                BuildCptStats(cptRaw);
        }

        private static string BuildCptStats(string cptRaw)
        {
            if (string.IsNullOrWhiteSpace(cptRaw) || cptRaw == "null") return "";

            try
            {
                using var doc = JsonDocument.Parse(cptRaw);
                var root = doc.RootElement;
                double accuracy = TryGetJsonDouble(root, "accuracy", out double acc) ? acc : 0;
                double hitRate = TryGetJsonDouble(root, "hit_rate", out double hit) ? hit : 0;
                double falseAlarmRate = TryGetJsonDouble(root, "false_alarm_rate", out double fa) ? fa : 0;
                double meanRt = TryGetJsonDouble(root, "mean_reaction_time_ms", out double rt) ? rt : 0;
                bool hasTau = TryGetJsonDouble(root, "tau_ms", out double tau) && tau > 0;

                return $"   ·   CPT {LanguageManager.T("Accuracy")} {FormatNumber(accuracy, "F1")}%   ·   " +
                       $"{LanguageManager.T("Mean RT")} {FormatNumber(meanRt, "F0")} ms   ·   " +
                       $"{LanguageManager.T("Hit Rate")} {FormatNumber(hitRate, "F2")}   ·   " +
                       $"{LanguageManager.T("False Alarm Rate")} {FormatNumber(falseAlarmRate, "F2")}" +
                       (hasTau ? $"   ·   τ {FormatNumber(tau, "F0")} ms" : "");
            }
            catch
            {
                return "";
            }
        }

        private void UpdateScientificMetrics(string cptRaw, string rrRaw, string attentionRaw)
        {
            UpdateCptTailMetric(cptRaw);
            UpdateHrvMetrics(rrRaw);
            UpdateAttentionMetric(attentionRaw);
        }

        private void UpdateCptTailMetric(string cptRaw)
        {
            CptTailText.Text = LanguageManager.T("No ex-Gaussian estimate available");
            if (string.IsNullOrWhiteSpace(cptRaw) || cptRaw == "null")
                return;

            try
            {
                using var doc = JsonDocument.Parse(cptRaw);
                JsonElement root = doc.RootElement;
                bool hasTau = TryGetJsonDouble(root, "tau_ms", out double tau) && tau > 0;
                bool hasMu = TryGetJsonDouble(root, "mu_ms", out double mu) && mu > 0;
                bool hasSigma = TryGetJsonDouble(root, "sigma_ms", out double sigma) && sigma > 0;
                int sampleCount = TryGetJsonDouble(root, "reaction_time_sample_count", out double count) && count >= 0
                    ? (int)Math.Round(count)
                    : 0;
                bool reliable = TryGetJsonBool(root, "ex_gaussian_reliable", out bool storedReliable) && storedReliable;

                if (!hasTau)
                    return;

                string reliability = reliable
                    ? LanguageManager.T("reporting threshold met")
                    : LanguageManager.T("exploratory: limited valid responses");
                CptTailText.Text = $"μ {FormatNumber(hasMu ? mu : 0, "F0")} ms   ·   σ {FormatNumber(hasSigma ? sigma : 0, "F0")} ms   ·   " +
                                   $"τ {FormatNumber(tau, "F0")} ms   ·   n={sampleCount} ({reliability})";
            }
            catch
            {
                // The historical details view remains usable for malformed legacy payloads.
            }
        }

        private void UpdateHrvMetrics(string rrRaw)
        {
            double[] rr = ParseValues(rrRaw).Where(HrvMetrics.IsValidRrInterval).ToArray();
            HrvMetricsResult hrv = HrvMetrics.Analyze(rr);
            if (!hrv.IsAvailable)
            {
                HrvMetricsText.Text = LanguageManager.T("No beat-to-beat RR intervals recorded");
                return;
            }

            string sdnn = hrv.SdnnMs.HasValue ? $"SDNN {FormatNumber(hrv.SdnnMs.Value, "F1")} ms" : "SDNN —";
            string rmssd = hrv.RmssdMs.HasValue ? $"RMSSD {FormatNumber(hrv.RmssdMs.Value, "F1")} ms" : "RMSSD —";
            string dfa = hrv.DfaAlpha1.HasValue ? $"DFA α1 {FormatNumber(hrv.DfaAlpha1.Value, "F2")}" : "DFA α1 —";
            string entropy = hrv.SampleEntropy.HasValue ? $"SampEn {FormatNumber(hrv.SampleEntropy.Value, "F2")}" : "SampEn —";
            HrvMetricsText.Text = $"{sdnn}   ·   {rmssd}\n{dfa}   ·   {entropy}   ·   n={hrv.RrSampleCount} RR";
        }

        private void UpdateAttentionMetric(string attentionRaw)
        {
            AttentionEstimateText.Text = LanguageManager.T("No posterior saved");
            AttentionIntervalText.Text = LanguageManager.T("Complete an assessment to estimate latent attention.");
            AttentionSourcesText.Text = "";
            if (string.IsNullOrWhiteSpace(attentionRaw) || attentionRaw == "null")
                return;

            try
            {
                using var doc = JsonDocument.Parse(attentionRaw);
                JsonElement root = doc.RootElement;
                if (!TryGetJsonDouble(root, "estimate", out double estimate) || estimate is < 0 or > 1)
                    return;

                double standardDeviation = TryGetJsonDouble(root, "posterior_standard_deviation", out double sd) && sd >= 0 ? sd : 0;
                double low = TryGetJsonDouble(root, "confidence_low", out double storedLow) ? storedLow : Math.Max(0, estimate - 1.96 * standardDeviation);
                double high = TryGetJsonDouble(root, "confidence_high", out double storedHigh) ? storedHigh : Math.Min(1, estimate + 1.96 * standardDeviation);
                int sources = TryGetJsonDouble(root, "observation_count", out double count) && count >= 0 ? (int)Math.Round(count) : 0;

                AttentionEstimateText.Text = $"{LanguageManager.T("Attention")} {FormatNumber(estimate, "F2")}  ·  {FormatNumber(estimate * 100, "F0")}/100";
                AttentionIntervalText.Text = $"95% {LanguageManager.T("model interval")}: {FormatNumber(low, "F2")} – {FormatNumber(high, "F2")}  (± {FormatNumber(1.96 * standardDeviation, "F2")})";
                AttentionSourcesText.Text = $"{sources} {LanguageManager.T("sensor observations fused with a scalar Kalman filter")}";
            }
            catch
            {
                // Keep the details page usable for a legacy or partially written session.
            }
        }

        private static bool TryGetJsonDouble(JsonElement root, string name, out double value)
        {
            value = 0;
            if (!root.TryGetProperty(name, out JsonElement property))
                return false;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value) && double.IsFinite(value))
                return true;
            return property.ValueKind == JsonValueKind.String &&
                   double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   double.IsFinite(value);
        }

        private static bool TryGetJsonBool(JsonElement root, string name, out bool value)
        {
            value = false;
            if (!root.TryGetProperty(name, out JsonElement property))
                return false;
            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            return property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out value);
        }

        
        
        
        private void DrawScatter(Canvas graph, Canvas xLbl, Canvas yLbl,
                                  List<Point> points)
        {
            graph.UpdateLayout();
            double w = graph.ActualWidth;
            double h = graph.ActualHeight;
            if (w < 1) { w = 740; h = 120; }

            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double padX = (maxX - minX) * 0.08 + 1;
            double padY = (maxY - minY) * 0.08 + 1;
            minX -= padX; maxX += padX;
            minY -= padY; maxY += padY;

            double rx = maxX - minX;
            double ry = maxY - minY;

            double Tx(double x) => (x - minX) / rx * w;
            double Ty(double y) => h - (y - minY) / ry * h;

            DrawGridLines(graph, xLbl, yLbl, minX, maxX, minY, maxY, w, h, Tx, Ty);
            DrawAxesLines(graph, minX, maxX, minY, maxY, w, h, Tx, Ty);

            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                double t = n > 1 ? (double)i / (n - 1) : 0.5;
                byte r = (byte)(77  + t * (255 - 77));
                byte g = (byte)(255 - t * (255 - 77));
                byte b = (byte)(223 - t * (223 - 255));

                var fill = new SolidColorBrush(Color.FromArgb(220, r, g, b));
                var glow = new SolidColorBrush(Color.FromArgb(55,  r, g, b));

                double cx = Tx(points[i].X);
                double cy = Ty(points[i].Y);

                var halo = new Ellipse { Width=10, Height=10, Fill=glow };
                Canvas.SetLeft(halo, cx-5); Canvas.SetTop(halo, cy-5);
                graph.Children.Add(halo);

                var dot = new Ellipse { Width=5, Height=5, Fill=fill };
                Canvas.SetLeft(dot, cx-2.5); Canvas.SetTop(dot, cy-2.5);
                graph.Children.Add(dot);
            }
        }

        
        
        
        private void DrawLineChart(Canvas graph, Canvas? xLbl, Canvas? yLbl,
                                    List<Point> points, LinearGradientBrush lineBrush,
                                    double? overrideMinX = null, double? overrideMaxX = null,
                                    double? overrideMinY = null, double? overrideMaxY = null,
                                    bool skipGrid = false)
        {
            if (points.Count < 2) return;

            graph.UpdateLayout();
            double w = graph.ActualWidth;
            double h = graph.ActualHeight;
            if (w < 1) { w = 740; h = 120; }

            double minX = overrideMinX ?? points.Min(p => p.X);
            double maxX = overrideMaxX ?? points.Max(p => p.X);
            double minY = overrideMinY ?? points.Min(p => p.Y);
            double maxY = overrideMaxY ?? points.Max(p => p.Y);

            if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }
            if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }

            double padY = (maxY - minY) * 0.10;
            minY -= padY; maxY += padY;

            double rx = maxX - minX;
            double ry = maxY - minY;

            double Tx(double x) => (x - minX) / rx * w;
            double Ty(double y) => h - (y - minY) / ry * h;

            if (!skipGrid)
                DrawGridLines(graph, xLbl, yLbl, minX, maxX, minY, maxY, w, h, Tx, Ty);

            var areaFig = new PathFigure
            {
                StartPoint = new Point(Tx(points[0].X), h),
                IsClosed   = true
            };
            areaFig.Segments.Add(new LineSegment(new Point(Tx(points[0].X), Ty(points[0].Y)), false));
            areaFig.Segments.Add(new PolyLineSegment(
                points.Skip(1).Select(p => new Point(Tx(p.X), Ty(p.Y))), true));
            areaFig.Segments.Add(new LineSegment(new Point(Tx(points[^1].X), h), false));

            var startColor = lineBrush.GradientStops[0].Color;
            var areaFill   = new SolidColorBrush(Color.FromArgb(14, startColor.R, startColor.G, startColor.B));
            graph.Children.Add(new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { areaFig }),
                Fill = areaFill
            });

            var lineFig = new PathFigure
            {
                StartPoint = new Point(Tx(points[0].X), Ty(points[0].Y))
            };
            lineFig.Segments.Add(new PolyLineSegment(
                points.Skip(1).Select(p => new Point(Tx(p.X), Ty(p.Y))), true));

            graph.Children.Add(new System.Windows.Shapes.Path
            {
                Data            = new PathGeometry(new[] { lineFig }),
                Stroke          = lineBrush,
                StrokeThickness = 1.5,
                StrokeLineJoin  = PenLineJoin.Round,
                Opacity         = 0.92
            });
        }

        
        
        
        private void DrawDistAxis(Canvas c, List<double> vals)
        {
            c.UpdateLayout();
            double w = c.ActualWidth;
            double h = c.ActualHeight;
            if (w < 1) { w = 740; h = 36; }

            int    n   = vals.Count;
            double mid = h / 2;

            c.Children.Add(new Line
            {
                X1=0, Y1=mid, X2=w, Y2=mid,
                Stroke          = new SolidColorBrush(Color.FromArgb(60, 160, 160, 255)),
                StrokeThickness = 1
            });

            foreach (double tx in new[] { 0.0, w })
                c.Children.Add(new Line
                {
                    X1=tx, Y1=mid-6, X2=tx, Y2=mid+6,
                    Stroke          = new SolidColorBrush(Color.FromArgb(70, 160, 160, 255)),
                    StrokeThickness = 1
                });

            var lblN = MakeLabel($"n={n}",
                new SolidColorBrush(Color.FromArgb(80, 160, 160, 255)), 8);
            Canvas.SetRight(lblN, 2); Canvas.SetTop(lblN, 2);
            c.Children.Add(lblN);

            var dotFill  = new SolidColorBrush(Color.FromArgb(210, 160, 160, 255));
            var haloFill = new SolidColorBrush(Color.FromArgb(22,  160, 160, 255));

            for (int i = 0; i < n; i++)
            {
                if (vals[i] < 0.5) continue;
                double cx = n > 1 ? (double)i / (n - 1) * w : w / 2;

                var halo = new Ellipse { Width=8, Height=8, Fill=haloFill };
                Canvas.SetLeft(halo, cx-4); Canvas.SetTop(halo, mid-4);
                c.Children.Add(halo);

                var dot = new Ellipse { Width=5, Height=5, Fill=dotFill };
                Canvas.SetLeft(dot, cx-2.5); Canvas.SetTop(dot, mid-2.5);
                c.Children.Add(dot);
            }
        }

        
        
        
        private static void DrawGridLines(Canvas graph, Canvas? xLbl, Canvas? yLbl,
            double minX, double maxX, double minY, double maxY,
            double w, double h,
            Func<double, double> Tx, Func<double, double> Ty)
        {
            var gridBrush = ThemeManager.IsDark
                ? new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(45, 15, 23, 42));
            var labelBrush = ThemeManager.IsDark
                ? new SolidColorBrush(Color.FromArgb(110, 100, 130, 160))
                : new SolidColorBrush(Color.FromArgb(150, 71, 85, 105));

            double stepX = NiceStep((maxX - minX) / 6);
            double stepY = NiceStep((maxY - minY) / 5);

            for (double v = Math.Ceiling(minX / stepX) * stepX; v <= maxX + stepX * 0.01; v += stepX)
            {
                double cx = Tx(v);
                graph.Children.Add(DashedLine(cx, 0, cx, h, gridBrush));
                if (xLbl != null)
                {
                    var lbl = MakeLabel(FormatNumber(v, "G4"), labelBrush, 8.5);
                    Canvas.SetLeft(lbl, cx - 14); Canvas.SetTop(lbl, h + 3);
                    xLbl.Children.Add(lbl);
                }
            }

            for (double v = Math.Ceiling(minY / stepY) * stepY; v <= maxY + stepY * 0.01; v += stepY)
            {
                double cy = Ty(v);
                graph.Children.Add(DashedLine(0, cy, w, cy, gridBrush));
                if (yLbl != null)
                {
                    var lbl = MakeLabel(FormatNumber(v, "G4"), labelBrush, 8.5);
                    Canvas.SetRight(lbl, 4); Canvas.SetTop(lbl, cy - 7);
                    yLbl.Children.Add(lbl);
                }
            }
        }

        private static void DrawAxesLines(Canvas graph,
            double minX, double maxX, double minY, double maxY,
            double w, double h,
            Func<double, double> Tx, Func<double, double> Ty)
        {
            var axisBrush = ThemeManager.IsDark
                ? new SolidColorBrush(Color.FromArgb(160, 77, 255, 223))
                : new SolidColorBrush(Color.FromArgb(170, 37, 99, 235));

            double yZero = (minY <= 0 && maxY >= 0) ? Ty(0) : h;
            graph.Children.Add(SolidLine(0, yZero, w, yZero, axisBrush, 1.5));

            double xZero = (minX <= 0 && maxX >= 0) ? Tx(0) : 0;
            graph.Children.Add(SolidLine(xZero, 0, xZero, h, axisBrush, 1.5));
        }

        
        
        

        private static List<Point> ParseXY(string raw)
        {
            var list = new List<Point>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var token in raw.Split(';'))
            {
                var parts = token.Trim().Split(',');
                if (parts.Length != 2) continue;
                if (TryInv(parts[0], out double x) && TryInv(parts[1], out double y))
                    list.Add(new Point(x, y));
            }
            return list;
        }

        private static List<Point> ParsePairs(string raw)
        {
            var list = new List<Point>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var token in raw.Split(';'))
            {
                var parts = token.Trim().Split(',');
                if (parts.Length != 2) continue;
                if (TryInv(parts[0], out double a) && TryInv(parts[1], out double b))
                    list.Add(new Point(a, b));
            }
            return list;
        }

        private static List<double> ParseValues(string raw)
        {
            var list = new List<double>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var token in raw.Split(','))
                if (TryInv(token.Trim(), out double v))
                    list.Add(v);
            return list;
        }

        private static List<Point> Indexed(List<double> vals) =>
            vals.Select((v, i) => new Point(i, v)).ToList();

        private static bool TryInv(string s, out double v) =>
            double.TryParse(s,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out v);

        private static CultureInfo UiCulture => LanguageManager.Current == AppLanguage.Romanian
            ? CultureInfo.GetCultureInfo("ro-RO")
            : CultureInfo.GetCultureInfo("en-US");

        private static string FormatNumber(double value, string format)
            => value.ToString(format, UiCulture);

        
        
        

        private static Line DashedLine(double x1, double y1, double x2, double y2, Brush stroke) =>
            new Line
            {
                X1=x1, Y1=y1, X2=x2, Y2=y2,
                Stroke=stroke, StrokeThickness=1,
                StrokeDashArray=new DoubleCollection { 3, 5 }
            };

        private static Line SolidLine(double x1, double y1, double x2, double y2,
                                       Brush stroke, double thickness) =>
            new Line { X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=stroke, StrokeThickness=thickness };

        private static TextBlock MakeLabel(string text, Brush fg, double size) =>
            new TextBlock
            {
                Text=text, Foreground=fg, FontSize=size,
                FontFamily=new FontFamily("Consolas")
            };

        private static LinearGradientBrush GradH(Color from, Color to) =>
            new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 0));

        private static double NiceStep(double raw)
        {
            if (raw <= 0) return 1;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f   = raw / mag;
            return f < 1.5 ? mag : f < 3.5 ? 2 * mag : f < 7.5 ? 5 * mag : 10 * mag;
        }
    }
}
