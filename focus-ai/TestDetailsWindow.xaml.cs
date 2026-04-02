using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace focus_ai
{
    public partial class TestDetailsWindow : Window
    {
        // ── Constructor ──────────────────────────────────────────────────────
        public TestDetailsWindow(string mapRaw, string ecgRaw,
                                  string spo2Raw, string hrRaw, string distRaw)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            Loaded += (_, _) => DrawAll(mapRaw, ecgRaw, spo2Raw, hrRaw, distRaw);
        }

        // ── Main dispatcher ──────────────────────────────────────────────────
        private void DrawAll(string mapRaw, string ecgRaw,
                              string spo2Raw, string hrRaw, string distRaw)
        {
            // MAP – scatter (original behaviour)
            var mapPoints = ParseXY(mapRaw);
            if (mapPoints.Count > 0)
                DrawScatter(MapCanvas, MapXLabels, MapYLabels, mapPoints);

            // ECG – two channels from pairs "x1,x2;…"
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
                    ch1, GradH(Color.FromRgb(77, 255, 223), Color.FromRgb(77, 200, 255)),
                    minX, maxX, minY, maxY);
                DrawLineChart(EcgCanvas1, null, null,
                    ch2, GradH(Color.FromRgb(255, 77, 200), Color.FromRgb(255, 140, 77)),
                    minX, maxX, minY, maxY, skipGrid: true);
            }

            // SPO2 – indexed values
            var spo2 = ParseValues(spo2Raw);
            if (spo2.Count > 0)
                DrawLineChart(Spo2Canvas, Spo2XLabels, Spo2YLabels,
                    Indexed(spo2),
                    GradH(Color.FromRgb(77, 159, 255), Color.FromRgb(77, 255, 223)));

            // HR – indexed values
            var hr = ParseValues(hrRaw);
            if (hr.Count > 0)
                DrawLineChart(HrCanvas, HrXLabels, HrYLabels,
                    Indexed(hr),
                    GradH(Color.FromRgb(255, 107, 77), Color.FromRgb(255, 215, 0)));

            // DIST – event axis
            var dist = ParseValues(distRaw);
            if (dist.Count > 0)
                DrawDistAxis(DistCanvas, dist);

            // Stats
            int mapN  = mapPoints.Count;
            double spo2Min = spo2.Any(v => v > 0) ? spo2.Where(v => v > 0).Min() : 0;
            double spo2Max = spo2.Any()            ? spo2.Max() : 0;
            double hrMin   = hr.Any(v => v > 0)   ? hr.Where(v => v > 0).Min() : 0;
            double hrMax   = hr.Any()              ? hr.Max() : 0;
            int distActive = dist.Count(v => v > 0);

            StatsText.Text =
                $"  MAP {mapN} pt   ·   " +
                $"ECG {ecgPairs.Count} pt   ·   " +
                $"SPO2 [{spo2Min:F0}–{spo2Max:F0}]%   ·   " +
                $"HR [{hrMin:F0}–{hrMax:F0}] bpm   ·   " +
                $"DIST {distActive}/{dist.Count} active";
        }

        // ════════════════════════════════════════════════════════════════════
        // SCATTER PLOT  (original MAP logic, unchanged)
        // ════════════════════════════════════════════════════════════════════
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

        // ════════════════════════════════════════════════════════════════════
        // LINE CHART  (ECG channels, SPO2, HR)
        // ════════════════════════════════════════════════════════════════════
        private void DrawLineChart(Canvas graph, Canvas xLbl, Canvas yLbl,
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
            var areaFill   = new SolidColorBrush(Color.FromArgb(18, startColor.R, startColor.G, startColor.B));
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
                StrokeThickness = 1.6,
                StrokeLineJoin  = PenLineJoin.Round,
                Opacity         = 0.92
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // DIST AXIS
        // ════════════════════════════════════════════════════════════════════
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
            var haloFill = new SolidColorBrush(Color.FromArgb(40,  160, 160, 255));

            for (int i = 0; i < n; i++)
            {
                if (vals[i] < 0.5) continue;
                double cx = n > 1 ? (double)i / (n - 1) * w : w / 2;

                var halo = new Ellipse { Width=10, Height=10, Fill=haloFill };
                Canvas.SetLeft(halo, cx-5); Canvas.SetTop(halo, mid-5);
                c.Children.Add(halo);

                var dot = new Ellipse { Width=5, Height=5, Fill=dotFill };
                Canvas.SetLeft(dot, cx-2.5); Canvas.SetTop(dot, mid-2.5);
                c.Children.Add(dot);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHARED GRID + AXES
        // ════════════════════════════════════════════════════════════════════
        private static void DrawGridLines(Canvas graph, Canvas xLbl, Canvas yLbl,
            double minX, double maxX, double minY, double maxY,
            double w, double h,
            Func<double, double> Tx, Func<double, double> Ty)
        {
            var gridBrush  = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            var labelBrush = new SolidColorBrush(Color.FromArgb(110, 100, 130, 160));

            double stepX = NiceStep((maxX - minX) / 6);
            double stepY = NiceStep((maxY - minY) / 5);

            for (double v = Math.Ceiling(minX / stepX) * stepX; v <= maxX + stepX * 0.01; v += stepX)
            {
                double cx = Tx(v);
                graph.Children.Add(DashedLine(cx, 0, cx, h, gridBrush));
                if (xLbl != null)
                {
                    var lbl = MakeLabel($"{v:G4}", labelBrush, 8.5);
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
                    var lbl = MakeLabel($"{v:G4}", labelBrush, 8.5);
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
            var axisBrush = new SolidColorBrush(Color.FromArgb(160, 77, 255, 223));

            double yZero = (minY <= 0 && maxY >= 0) ? Ty(0) : h;
            graph.Children.Add(SolidLine(0, yZero, w, yZero, axisBrush, 1.5));

            double xZero = (minX <= 0 && maxX >= 0) ? Tx(0) : 0;
            graph.Children.Add(SolidLine(xZero, 0, xZero, h, axisBrush, 1.5));
        }

        // ════════════════════════════════════════════════════════════════════
        // PARSERS
        // ════════════════════════════════════════════════════════════════════

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

        // ════════════════════════════════════════════════════════════════════
        // VISUAL HELPERS
        // ════════════════════════════════════════════════════════════════════

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