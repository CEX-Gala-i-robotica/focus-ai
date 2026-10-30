
using System;
using System.Diagnostics;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
        private bool _spaceDown;

        public BuzzerTest(bool isDark)
        {
            InitializeComponent();

            LanguageManager.Register(this);
            WindowHelper.MoveToSecondMonitor(this);

            _isDark = isDark;

            ThemeManager.Apply(_isDark);

            _uiTimer.Interval = TimeSpan.FromMilliseconds(10);

            _uiTimer.Tick += (s, e) =>
            {
                TxtTimer.Text =
                    _stopwatch.Elapsed.TotalSeconds.ToString("F3") + " s";
            };

            PreviewKeyDown += BuzzerTest_PreviewKeyDown;
            PreviewKeyUp += BuzzerTest_PreviewKeyUp;

            Loaded += (_, _) => Keyboard.Focus(this);

            SetStatus(
                LanguageManager.T("Press Start to begin the test"),
                StatusColor.Blue);
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

            SetStatus(
                LanguageManager.T("Wait for the sound cue..."),
                StatusColor.Orange);

            int waitTime = _random.Next(5000, 15001);

            await Task.Delay(waitTime);

            if (!IsLoaded || _finished)
                return;

            try
            {
                SystemSounds.Beep.Play();

                _stopwatch.Start();
                _uiTimer.Start();

                Keyboard.Focus(this);

                SetStatus(
                    LanguageManager.T("PRESS SPACE NOW!"),
                    StatusColor.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    LanguageManager.T("Error:") + " " + ex.Message);

                BtnStart.IsEnabled = true;
                _testStarted = false;
            }
        }

        private void BuzzerTest_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Space || !_testStarted)
                return;

            e.Handled = true;

            if (_spaceDown)
                return;

            _spaceDown = true;

            HandleSpacePress();
        }

        private void BuzzerTest_PreviewKeyUp(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                _spaceDown = false;
        }

        private void HandleSpacePress()
        {
            if (!_testStarted ||
                _finished ||
                !_stopwatch.IsRunning)
            {
                return;
            }

            _finished = true;

            _stopwatch.Stop();

            ReactionTime = _stopwatch.Elapsed.TotalSeconds;

            _uiTimer.Stop();

            double elapsed = _stopwatch.Elapsed.TotalSeconds;

            TxtTimer.Text = elapsed.ToString("F3") + " s";

            SetStatus(
                $"{LanguageManager.T("Nice! Time:")} {elapsed:F3} s",
                StatusColor.Blue);

            MessageBox.Show(
                $"{LanguageManager.T("Result:")} {elapsed:F3} " +
                $"{LanguageManager.T("seconds")}",
                LanguageManager.T("Test completed"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close();
        }

        private enum StatusColor
        {
            Blue,
            Green,
            Orange
        }

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
            _stopwatch.Stop();

            base.OnClosed(e);
        }
    }
}
