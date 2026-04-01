using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace focus_ai
{
    public class StimulusEvent
    {
        public int Index { get; set; }
        public bool IsGo { get; set; }
        public double OnsetMs { get; set; }
        public double? ReactionMs { get; set; }
        public bool WasCorrect { get; set; }
    }

    public class GoNoGoResult
    {
        public List<StimulusEvent> Events { get; set; } = new();
        public int CorrectGo { get; set; }
        public int MissedGo { get; set; }
        public int FalsePositives { get; set; }
        public int CorrectRejects { get; set; }
        public double AverageReactionMs { get; set; }
        public List<EcgSample> Ecg => BioCollector.Instance.Ecg;
    }

    public partial class GoNoGoTest : Window
    {
        private const int TEST_DURATION_SEC = 30;
        private const int MIN_STIM_MS = 600;
        private const int MAX_STIM_MS = 1400;
        private const int MIN_ISI_MS = 300;
        private const int MAX_ISI_MS = 900;
        private const double GO_PROBABILITY = 0.65;
        public double Accuracy { get; private set; }
        private readonly DispatcherTimer _countdownTimer = new();
        private readonly DispatcherTimer _stimulusTimer = new();

        private int _secondsLeft = TEST_DURATION_SEC;
        private DateTime _testStart;

        private readonly Random _rng = new();
        private bool _stimulusActive;
        private bool _currentIsGo;
        private DateTime _stimulusOnset;
        private bool _responseReceived;
        private int _stimulusIndex;
        private bool _testRunning;
        private Storyboard? _pulseAnim;

        private readonly List<StimulusEvent> _events = new();
        private readonly List<double> _reactionTimes = new();
        private int _correctGo;
        private int _missedGo;
        private int _falsePositives;
        private int _correctRejects;

        private static readonly Color GoColor = Color.FromRgb(22, 197, 94);
        private static readonly Color NoGoColor = Color.FromRgb(239, 68, 68);
        private static readonly Color IdleColor = Color.FromRgb(31, 41, 55);

        public GoNoGoTest(bool isDark)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            ThemeManager.Apply(isDark);

            _pulseAnim = (Storyboard)Resources["PulseAnim"];

            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;
            _stimulusTimer.Tick += StimulusTimer_Tick;

            BioCollector.Instance.TouchDetected += OnTouchDetected;

            KeyDown += (_, ke) =>
            {
                if (ke.Key == System.Windows.Input.Key.Space ||
                    ke.Key == System.Windows.Input.Key.Enter)
                {
                    HandleTouch();
                }
            };

            Closed += (_, _) =>
            {
                BioCollector.Instance.TouchDetected -= OnTouchDetected;
                StopAll();
            };
        }

        private void OnTouchDetected() => HandleTouch();

        private void HandleTouch()
        {
            if (!_testRunning || !_stimulusActive || _responseReceived)
                return;

            ResolveCurrentStimulus(touched: true);
        }

        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {
            OverlayPanel.Visibility = Visibility.Collapsed;
            StartTest();
        }

        private void StartTest()
        {
            _testRunning = true;
            _testStart   = DateTime.Now;
            _secondsLeft = TEST_DURATION_SEC;

            CountdownText.Text         = _secondsLeft.ToString();
            TimerStatusText.Text       = "În desfășurare";
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

            // 🔥 IMPORTANT
            BioCollector.Instance.Send("START_TEST");

            _countdownTimer.Start();
            ScheduleNextISI();
        }

        private void StopAll()
        {
            _testRunning = false;
            _countdownTimer.Stop();
            _stimulusTimer.Stop();
            _pulseAnim?.Stop();
        }

        private void FinishTest()
        {
            StopAll();

            // 🔥 IMPORTANT
            BioCollector.Instance.Send("STOP_TEST");

            ShowCircle(IdleColor, "✓", "Finalizat");
            int total = _correctGo + _missedGo + _falsePositives + _correctRejects;
            Accuracy = total > 0 ? (double)(_correctGo + _correctRejects) / total * 100.0 : 0;
            ShowResultOverlay(BuildResult());
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            CountdownText.Text = _secondsLeft.ToString();

            if (_secondsLeft <= 0)
                FinishTest();
            else if (_secondsLeft <= 5)
                CountdownText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void ScheduleNextISI()
        {
            if (!_testRunning) return;

            _stimulusActive = false;
            _responseReceived = false;

            ShowCircle(IdleColor, "·", "");

            _stimulusTimer.Interval = TimeSpan.FromMilliseconds(
                _rng.Next(MIN_ISI_MS, MAX_ISI_MS + 1));

            _stimulusTimer.Stop();
            _stimulusTimer.Start();
        }

        private void StimulusTimer_Tick(object? sender, EventArgs e)
        {
            _stimulusTimer.Stop();
            if (!_testRunning) return;

            if (!_stimulusActive)
            {
                ShowNextStimulus();
            }
            else
            {
                ResolveCurrentStimulus(touched: false);
            }
        }

        private void ShowNextStimulus()
        {
            _stimulusTimer.Stop();

            _currentIsGo = _rng.NextDouble() < GO_PROBABILITY;
            _stimulusActive = true;
            _responseReceived = false;
            _stimulusOnset = DateTime.Now;

            _pulseAnim?.Stop();
            PulseRing1.Opacity = 0;
            PulseRing2.Opacity = 0;

            // Oprește orice animație activă pe culoare
            CircleBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            if (_currentIsGo)
            {
                CircleBrush.Color = GoColor;   // VERDE forțat direct
                StimulusLabel.Text = "GO";
                StimulusSubLabel.Text = "Apasă!";
                _pulseAnim?.Begin();
            }
            else
            {
                CircleBrush.Color = NoGoColor;  // ROȘU forțat direct
                StimulusLabel.Text = "NO-GO";
                StimulusSubLabel.Text = "Nu apăsa";
            }

            _stimulusTimer.Interval = TimeSpan.FromMilliseconds(
                _rng.Next(MIN_STIM_MS, MAX_STIM_MS + 1));

            _stimulusTimer.Start();
        }

        private void ShowCircle(Color fill, string label, string sub)
        {
            CircleBrush.Color = fill;
            StimulusLabel.Text = label;
            StimulusSubLabel.Text = sub;
        }

        private void FlashCircle(Color flashColor, Color returnColor)
        {
            var anim = new ColorAnimation
            {
                From = flashColor,
                To = returnColor,
                Duration = TimeSpan.FromMilliseconds(150),
            };
            CircleBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        private void ResolveCurrentStimulus(bool touched)
        {
            if (!_stimulusActive)
                return;

            _stimulusTimer.Stop();

            double? rtMs = null;
            bool wasCorrect;

            if (touched)
            {
                rtMs = (DateTime.Now - _stimulusOnset).TotalMilliseconds;
                _responseReceived = true;

                if (_currentIsGo)
                {
                    _correctGo++;
                    _reactionTimes.Add(rtMs.Value);
                    // apăsat corect pe GO (verde) – flash alb apoi rămâne verde
                    FlashCircle(Colors.White, GoColor);
                    wasCorrect = true;
                }
                else
                {
                    _falsePositives++;
                    // apăsat greșit pe NO-GO (roșu) – flash alb apoi rămâne roșu
                    FlashCircle(Colors.White, NoGoColor);
                    wasCorrect = false;
                }
            }
            else
            {
                if (_currentIsGo)
                {
                    _missedGo++;
                    wasCorrect = false;
                }
                else
                {
                    _correctRejects++;
                    wasCorrect = true;
                }
            }

            _events.Add(new StimulusEvent
            {
                Index      = _stimulusIndex++,
                IsGo       = _currentIsGo,
                OnsetMs    = (_stimulusOnset - _testStart).TotalMilliseconds,
                ReactionMs = rtMs,
                WasCorrect = wasCorrect
            });

            _stimulusActive = false;

            _pulseAnim?.Stop();
            PulseRing1.Opacity = 0;
            PulseRing2.Opacity = 0;

            RefreshScoreboard();

            if (_testRunning)
                ScheduleNextISI();
        }

        private void RefreshScoreboard()
        {
            CorrectGoText.Text = _correctGo.ToString();
            FalsePosText.Text = _falsePositives.ToString();
            AvgRtText.Text = _reactionTimes.Count > 0
                ? $"{(int)_reactionTimes.Average()} ms"
                : "-- ms";
        }

        private GoNoGoResult BuildResult() => new()
        {
            Events = new List<StimulusEvent>(_events),
            CorrectGo = _correctGo,
            MissedGo = _missedGo,
            FalsePositives = _falsePositives,
            CorrectRejects = _correctRejects,
            AverageReactionMs = _reactionTimes.Count > 0 ? _reactionTimes.Average() : 0
        };

        private void ShowResultOverlay(GoNoGoResult r)
        {
            OverlayTitle.Text = "Test finalizat!";
            OverlayBody.Text =
                $"GO corecte: {r.CorrectGo}   |   Omisiuni: {r.MissedGo}\n" +
                $"False pozitive: {r.FalsePositives}   |   NO-GO corecte: {r.CorrectRejects}\n" +
                $"Timp mediu reacție: {(r.AverageReactionMs > 0 ? $"{(int)r.AverageReactionMs} ms" : "N/A")}";

            BeginButton.Content = "✕  Închide";
            BeginButton.Click -= BeginButton_Click;
            BeginButton.Click += (_, _) => Close();

            OverlayPanel.Visibility = Visibility.Visible;
        }
    }
}