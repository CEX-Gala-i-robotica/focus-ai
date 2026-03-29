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
        private double _canvasW;
        private double _canvasH;

        public TestDetailsWindow(string mapRaw)
        {
            InitializeComponent();
            Loaded += (_, _) => Draw(mapRaw);
        }

        private void Draw(string mapRaw)
        {
            if (string.IsNullOrWhiteSpace(mapRaw)) return;

            _canvasW = GraphCanvas.ActualWidth;
            _canvasH = GraphCanvas.ActualHeight;

            var points = ParsePoints(mapRaw);
            if (points.Count == 0) return;

            // Compute data bounds with padding
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double padX = (maxX - minX) * 0.08 + 1;
            double padY = (maxY - minY) * 0.08 + 1;

            minX -= padX; maxX += padX;
            minY -= padY; maxY += padY;

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            // Map data coords -> canvas coords
            double ToCanvasX(double x) => (x - minX) / rangeX * _canvasW;
            double ToCanvasY(double y) => _canvasH - (y - minY) / rangeY * _canvasH;

            DrawGrid(minX, maxX, minY, maxY, ToCanvasX, ToCanvasY);
            DrawAxes(minX, maxX, minY, maxY, ToCanvasX, ToCanvasY);
            DrawPoints(points, ToCanvasX, ToCanvasY);

            StatsText.Text = $"  {points.Count} puncte   ·   " +
                             $"X [{points.Min(p => p.X):F1} … {points.Max(p => p.X):F1}]   ·   " +
                             $"Y [{points.Min(p => p.Y):F1} … {points.Max(p => p.Y):F1}]";
        }

        // ── Parse ────────────────────────────────────────────────────────────
        private static List<Point> ParsePoints(string raw)
        {
            var list = new List<Point>();
            foreach (var token in raw.Split(';'))
            {
                var xy = token.Trim().Split(',');
                if (xy.Length != 2) continue;
                if (!double.TryParse(xy[0].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double x)) continue;
                if (!double.TryParse(xy[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double y)) continue;
                list.Add(new Point(x, y));
            }
            return list;
        }

        // ── Grid ─────────────────────────────────────────────────────────────
        private void DrawGrid(double minX, double maxX, double minY, double maxY,
                               Func<double, double> toX, Func<double, double> toY)
        {
            double stepX = NiceStep((maxX - minX) / 6);
            double stepY = NiceStep((maxY - minY) / 6);

            var gridColor = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            var labelColor = new SolidColorBrush(Color.FromArgb(120, 100, 130, 160));

            // Vertical grid lines + X labels
            for (double v = Math.Ceiling(minX / stepX) * stepX; v <= maxX; v += stepX)
            {
                double cx = toX(v);
                var line = new Line
                {
                    X1 = cx, Y1 = 0, X2 = cx, Y2 = _canvasH,
                    Stroke = gridColor, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 4 }
                };
                GraphCanvas.Children.Add(line);

                var lbl = MakeLabel($"{v:G4}", labelColor, 9);
                Canvas.SetLeft(lbl, cx - 16);
                Canvas.SetTop(lbl, _canvasH + 4);
                XLabelsCanvas.Children.Add(lbl);
            }

            // Horizontal grid lines + Y labels
            for (double v = Math.Ceiling(minY / stepY) * stepY; v <= maxY; v += stepY)
            {
                double cy = toY(v);
                var line = new Line
                {
                    X1 = 0, Y1 = cy, X2 = _canvasW, Y2 = cy,
                    Stroke = gridColor, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 4 }
                };
                GraphCanvas.Children.Add(line);

                var lbl = MakeLabel($"{v:G4}", labelColor, 9);
                Canvas.SetRight(lbl, 4);
                Canvas.SetTop(lbl, cy - 8);
                YLabelsCanvas.Children.Add(lbl);
            }
        }

        // ── Axes ─────────────────────────────────────────────────────────────
        private void DrawAxes(double minX, double maxX, double minY, double maxY,
                               Func<double, double> toX, Func<double, double> toY)
        {
            var axisBrush = new SolidColorBrush(Color.FromArgb(180, 77, 255, 223));

            // X axis (y=0 if visible, else bottom)
            double yZero = (minY <= 0 && maxY >= 0) ? toY(0) : _canvasH;
            AddLine(0, yZero, _canvasW, yZero, axisBrush, 1.5);

            // Y axis (x=0 if visible, else left)
            double xZero = (minX <= 0 && maxX >= 0) ? toX(0) : 0;
            AddLine(xZero, 0, xZero, _canvasH, axisBrush, 1.5);
        }

        // ── Points ───────────────────────────────────────────────────────────
        private void DrawPoints(List<Point> points,
                                 Func<double, double> toX, Func<double, double> toY)
        {
            // Color gradient by index: cyan → magenta
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                double t = n > 1 ? (double)i / (n - 1) : 0.5;

                byte r = (byte)(77  + t * (255 - 77));
                byte g = (byte)(255 - t * (255 - 77));
                byte b = (byte)(223 - t * (223 - 255));

                var fill = new SolidColorBrush(Color.FromArgb(220, r, g, b));
                var glow = new SolidColorBrush(Color.FromArgb(60,  r, g, b));

                double cx = toX(points[i].X);
                double cy = toY(points[i].Y);

                // Glow halo
                var halo = new Ellipse { Width = 10, Height = 10, Fill = glow };
                Canvas.SetLeft(halo, cx - 5);
                Canvas.SetTop(halo,  cy - 5);
                GraphCanvas.Children.Add(halo);

                // Core dot
                var dot = new Ellipse { Width = 5, Height = 5, Fill = fill };
                Canvas.SetLeft(dot, cx - 2.5);
                Canvas.SetTop(dot,  cy - 2.5);
                GraphCanvas.Children.Add(dot);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void AddLine(double x1, double y1, double x2, double y2,
                              Brush stroke, double thickness)
        {
            GraphCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = thickness
            });
        }

        private static TextBlock MakeLabel(string text, Brush foreground, double size) =>
            new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = size,
                FontFamily = new FontFamily("Consolas")
            };

        /// Round to a "nice" axis step: 1, 2, 5, 10, 20, 50 …
        private static double NiceStep(double raw)
        {
            if (raw <= 0) return 1;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f = raw / mag;
            return f < 1.5 ? mag : f < 3.5 ? 2 * mag : f < 7.5 ? 5 * mag : 10 * mag;
        }
    }
}