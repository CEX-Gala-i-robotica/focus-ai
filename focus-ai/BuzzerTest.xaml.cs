using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace focus_ai
{
    public partial class BuzzerTest : Window
    {
        private readonly Stopwatch _stopwatch = new();
        private readonly DispatcherTimer _uiTimer = new();
        private readonly Random _random = new();
        public double? ReactionTime { get; private set; }
        private readonly bool _isDark;
        private bool _testStarted;
        private bool _finished;

        public BuzzerTest(bool isDark)
        {
            InitializeComponent();

            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _uiTimer.Interval = TimeSpan.FromMilliseconds(10);
            _uiTimer.Tick += (s, e) =>
                TxtTimer.Text = _stopwatch.Elapsed.TotalSeconds.ToString("F3") + " s";

            BioCollector.Instance.TouchDetected += OnTouchDetected;
            SetStatus("Apasă Start pentru a porni testul", StatusColor.Blue);
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_testStarted)
                return;

            _testStarted = true;
            _finished = false;

            BtnStart.IsEnabled = false;
            TxtTimer.Text = "0.000 s";
            _stopwatch.Reset();

            SetStatus("Așteaptă semnalul sonor...", StatusColor.Orange);

            int waitTime = _random.Next(5000, 15001);
            await Task.Delay(waitTime);

            try
            {
                BioCollector.Instance.Send("START_TEST");

                BioCollector.Instance.Send("BEEP"); // 🔥 DOAR AICI SUNĂ

                _stopwatch.Start();
                _uiTimer.Start();

                SetStatus("APASĂ PE SENZOR ACUM!", StatusColor.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message);
                BtnStart.IsEnabled = true;
                _testStarted = false;
            }
        }

        private void OnTouchDetected()
        {
            if (!_testStarted || _finished || !_stopwatch.IsRunning)
                return;

            _finished = true;
            _stopwatch.Stop();
            ReactionTime = _stopwatch.Elapsed.TotalSeconds;
            _uiTimer.Stop();

            BioCollector.Instance.Send("STOP_TEST");

            double elapsed = _stopwatch.Elapsed.TotalSeconds;

            Dispatcher.Invoke(() =>
            {
                TxtTimer.Text = elapsed.ToString("F3") + " s";
                SetStatus($"Bravo! Timp: {elapsed:F3} s", StatusColor.Blue);

                MessageBox.Show($"Rezultat: {elapsed:F3} secunde", "Test finalizat");
                Close();
            });
        }

        private enum StatusColor { Blue, Green, Orange }

        private void SetStatus(string text, StatusColor color)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(text, color));
                return;
            }

            TxtInstruction.Text = text;

            var (bgKey, fgKey) = color switch
            {
                StatusColor.Green => ("BgStatGreen", "TxtStatGreen"),
                StatusColor.Orange => ("BgStatOrange", "TxtStatOrange"),
                _ => ("BgStatBlue", "TxtStatBlue")
            };

            StatusBadge.Background = (Brush)FindResource(bgKey);
            TxtInstruction.Foreground = (Brush)FindResource(fgKey);
        }

        protected override void OnClosed(EventArgs e)
        {
            _uiTimer.Stop();
            BioCollector.Instance.TouchDetected -= OnTouchDetected;

            if (_testStarted && !_finished)
                BioCollector.Instance.Send("STOP_TEST");

            base.OnClosed(e);
        }
    }
}