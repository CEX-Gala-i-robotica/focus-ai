
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
        private bool _cueStarted;
        private uint? _arduinoCueTimestampMs;

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
            BioCollector.Instance.BuzzerStarted += BioCollector_BuzzerStarted;
            BioCollector.Instance.TouchDetected += BioCollector_TouchDetected;

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
                _cueStarted = false;
                _arduinoCueTimestampMs = null;

                // Prefer the physical Arduino buzzer. If no compatible device is
                // connected or it does not acknowledge the cue, fall back to the
                // Windows beep and keyboard response.
                if (BioCollector.Instance.TriggerBuzzer())
                {
                    SetStatus(LanguageManager.T("Waiting for the hardware cue..."), StatusColor.Orange);
                    await Task.Delay(450);
                    if (!IsLoaded || _finished || _cueStarted)
                        return;
                }

                SystemSounds.Beep.Play();
                BeginCue(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    LanguageManager.T("Error:") + " " + ex.Message);

                BtnStart.IsEnabled = true;
                _testStarted = false;
            }
        }

        private void BioCollector_BuzzerStarted(uint deviceTimestampMs)
        {
            if (!IsLoaded || _finished || _cueStarted)
                return;

            _arduinoCueTimestampMs = deviceTimestampMs;
            BeginCue(deviceTimestampMs);
        }

        private void BioCollector_TouchDetected(uint deviceTimestampMs)
        {
            if (!IsLoaded || _finished || !_cueStarted || !_arduinoCueTimestampMs.HasValue)
                return;

            // Unsigned subtraction is wrap-safe for Arduino millis() rollover.
            uint elapsedMs = unchecked(deviceTimestampMs - _arduinoCueTimestampMs.Value);
            if (elapsedMs > 15000)
                return;

            CompleteReaction(elapsedMs / 1000.0, "touch sensor");
        }

        private void BeginCue(uint? arduinoTimestampMs)
        {
            if (_cueStarted || _finished)
                return;

            _arduinoCueTimestampMs = arduinoTimestampMs;
            _cueStarted = true;
            _stopwatch.Start();
            _uiTimer.Start();
            Keyboard.Focus(this);
            SetStatus(
                LanguageManager.T("PRESS SPACE NOW OR TOUCH THE SENSOR!"),
                StatusColor.Green);
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
            if (!_testStarted || _finished || !_stopwatch.IsRunning)
                return;

            CompleteReaction(_stopwatch.Elapsed.TotalSeconds, "keyboard");
        }

        private void CompleteReaction(double elapsed, string responseSource)
        {
            if (_finished || !_cueStarted || !double.IsFinite(elapsed) || elapsed < 0)
                return;

            _finished = true;
            _stopwatch.Stop();
            ReactionTime = elapsed;
            _uiTimer.Stop();
            TxtTimer.Text = elapsed.ToString("F3") + " s";

            SetStatus(
                $"{LanguageManager.T("Nice! Time:")} {elapsed:F3} s ({responseSource})",
                StatusColor.Blue);

            MessageBox.Show(
                $"{LanguageManager.T("Result:")} {elapsed:F3} " +
                $"{LanguageManager.T("seconds")} ({responseSource})",
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
            BioCollector.Instance.BuzzerStarted -= BioCollector_BuzzerStarted;
            BioCollector.Instance.TouchDetected -= BioCollector_TouchDetected;

            base.OnClosed(e);
        }
    }
}
