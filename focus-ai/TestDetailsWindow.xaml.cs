using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace focus_ai
{
    public partial class TestDetailsWindow : Window
    {
        public TestDetailsWindow(string mapRaw)
        {
            InitializeComponent();
            Draw(mapRaw);
        }

        private void Draw(string mapRaw)
        {
            if (string.IsNullOrWhiteSpace(mapRaw))
                return;

            var pts = mapRaw.Split(';');

            foreach (var p in pts)
            {
                var xy = p.Trim().Split(',');

                if (xy.Length != 2) continue;

                if (!double.TryParse(xy[0], out double x)) continue;
                if (!double.TryParse(xy[1], out double y)) continue;

                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = Brushes.Cyan
                };

                // invers Y pentru coordonate matematice
                Canvas.SetLeft(dot, x);
                Canvas.SetTop(dot, 400 - y);

                GraphCanvas.Children.Add(dot);
            }

            DrawAxes();
        }

        private void DrawAxes()
        {
            var xAxis = new Line
            {
                X1 = 0,
                Y1 = 400,
                X2 = 400,
                Y2 = 400,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };

            var yAxis = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = 400,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };

            GraphCanvas.Children.Add(xAxis);
            GraphCanvas.Children.Add(yAxis);
        }
    }
}