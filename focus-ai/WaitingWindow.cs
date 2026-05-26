using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace focus_ai
{
    public class WaitingWindow : Window
    {
        public WaitingWindow()
        {
            Title = "Focus AI v2.0";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(8, 13, 23));
            Foreground = Brushes.White;

            var root = new Grid
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(8, 13, 23),
                    Color.FromRgb(18, 28, 48),
                    90)
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(48)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Focus AI v2.0",
                FontSize = 54,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Wait for the doctor's instructions",
                FontSize = 28,
                Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 900,
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            root.Children.Add(panel);
            Content = root;

            LanguageManager.Register(this);
        }
    }
}
