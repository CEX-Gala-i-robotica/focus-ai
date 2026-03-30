using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class MatematicaGame : Window
    {
        private const int GameDuration = 60;
        private const string RegPath = @"Software\FocusAI";
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private readonly bool _isDark;

        private enum Difficulty { Easy, Medium, Hard }
        private Difficulty _difficulty;
        private string _difficultyLabel = "";

        private int _score;
        private int _correct;
        private int _wrong;
        private int _streak;
        private int _bestStreak;
        private int _secondsLeft;
        private int _correctAnswer;
        private bool _gameActive;

        private readonly Random _rng = new();
        private readonly DispatcherTimer _timer = new();
        private DateTime _startTime;

        public MatematicaGame(bool isDark)
        {
            InitializeComponent();
            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            SizeChanged += (_, _) => { if (_gameActive) UpdateProgressBar(); };
        }

        private void BtnEasy_Click(object s, RoutedEventArgs e)   => StartGame(Difficulty.Easy,   "Ușor");
        private void BtnMedium_Click(object s, RoutedEventArgs e) => StartGame(Difficulty.Medium, "Mediu");
        private void BtnHard_Click(object s, RoutedEventArgs e)   => StartGame(Difficulty.Hard,   "Dificil");

        private void StartGame(Difficulty diff, string label)
        {
            _difficulty      = diff;
            _difficultyLabel = label;
            _score           = 0;
            _correct         = 0;
            _wrong           = 0;
            _streak          = 0;
            _bestStreak      = 0;
            _secondsLeft     = GameDuration;
            _gameActive      = true;
            _startTime       = DateTime.Now;

            WinOverlay.Visibility = Visibility.Collapsed;
            TimerBorder.Background = (SolidColorBrush)FindResource("BgStatBlue");

            UpdateHUD();
            PanelDifficulty.Visibility = Visibility.Collapsed;
            PanelGame.Visibility       = Visibility.Visible;

            NextQuestion();
            _timer.Start();
            TxtAnswer.Focus();
        }

        private void Timer_Tick(object? s, EventArgs e)
        {
            _secondsLeft--;
            TxtTimer.Text = _secondsLeft.ToString();
            UpdateProgressBar();

            if (_secondsLeft <= 10)
                TimerBorder.Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));

            if (_secondsLeft <= 0)
                EndGame();
        }

        private void UpdateProgressBar()
        {
            double parentWidth = ((Border)ProgressBar.Parent).ActualWidth;
            double pct         = (double)_secondsLeft / GameDuration;
            ProgressBar.Width  = Math.Max(0, parentWidth * pct);
        }

        private void NextQuestion()
        {
            TxtFeedback.Opacity = 0;
            TxtAnswer.Text = "";
            TxtAnswer.Focus();

            (int a, int b, char op) = GenerateQuestion();
            _correctAnswer = Compute(a, b, op);
            string opStr   = op switch { '*' => "×", '/' => "÷", _ => op.ToString() };
            TxtQuestion.Text = $"{a}  {opStr}  {b}";
        }

        private (int a, int b, char op) GenerateQuestion()
        {
            char[] ops = _difficulty switch
            {
                Difficulty.Easy   => new[] { '+', '-' },
                Difficulty.Medium => new[] { '+', '-', '*' },
                _                 => new[] { '+', '-', '*', '/' }
            };
            int maxVal = _difficulty switch
            {
                Difficulty.Easy   => 20,
                Difficulty.Medium => 50,
                _                 => 100
            };

            char op = ops[_rng.Next(ops.Length)];
            int a, b;

            if (op == '/')
            {
                b = _rng.Next(2, maxVal / 4 + 1);
                a = b * _rng.Next(2, maxVal / b + 1);
            }
            else if (op == '-')
            {
                a = _rng.Next(1, maxVal + 1);
                b = _rng.Next(1, a + 1);
            }
            else
            {
                a = _rng.Next(1, maxVal + 1);
                b = _rng.Next(1, (op == '*' ? Math.Min(maxVal, 12) : maxVal) + 1);
            }

            return (a, b, op);
        }

        private static int Compute(int a, int b, char op) => op switch
        {
            '+' => a + b,
            '-' => a - b,
            '*' => a * b,
            '/' => a / b,
            _   => 0
        };

        private void TxtAnswer_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _gameActive) Submit();
        }

        private void TxtAnswer_PreviewTextInput(object s, TextCompositionEventArgs e)
        {
            bool ok = char.IsDigit(e.Text, 0) || (e.Text == "-" && TxtAnswer.Text.Length == 0);
            e.Handled = !ok;
        }

        private void BtnSubmit_Click(object s, RoutedEventArgs e)
        {
            if (_gameActive) Submit();
        }

        private void Submit()
        {
            if (!int.TryParse(TxtAnswer.Text, out int answer)) return;

            if (answer == _correctAnswer)
            {
                _correct++;
                _streak++;
                if (_streak > _bestStreak) _bestStreak = _streak;
                int pts = 10 + (_streak >= 5 ? 5 : 0) + (_streak >= 10 ? 5 : 0);
                _score += pts;
                ShowFeedback("✓", "#22C55E");
            }
            else
            {
                _wrong++;
                _streak = 0;
                _score  = Math.Max(0, _score - 3);
                ShowFeedback($"✗  Răspuns: {_correctAnswer}", "#EF4444");
            }

            UpdateHUD();
            NextQuestion();
        }

        private void ShowFeedback(string text, string colorHex)
        {
            TxtFeedback.Text       = text;
            TxtFeedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            TxtFeedback.Opacity    = 1;
            var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(900)));
            TxtFeedback.BeginAnimation(OpacityProperty, anim);
        }

        private void UpdateHUD()
        {
            TxtScore.Text  = _score.ToString();
            TxtStreak.Text = _streak.ToString();
            TxtTimer.Text  = _secondsLeft.ToString();

            int total        = _correct + _wrong;
            TxtCorrect.Text  = _correct.ToString();
            TxtWrong.Text    = _wrong.ToString();
            TxtAccuracy.Text = total > 0 ? $"{(int)((double)_correct / total * 100)}%" : "—";
        }

        // ═══════════════════════════════════════════════════
        //  END GAME — overlay identic cu MemorieGame
        // ═══════════════════════════════════════════════════
        private void EndGame()
        {
            _timer.Stop();
            _gameActive = false;

            int total       = _correct + _wrong;
            double accuracy = total > 0 ? (double)_correct / total : 0;
            double finalScore = Math.Round(Math.Min(100, (accuracy * 60) + (_correct * 1.2) + (_bestStreak * 0.5)), 2);

            string emoji = finalScore >= 80 ? "🏆"
                         : finalScore >= 50 ? "👍"
                         : "💪";
            string title = finalScore >= 80 ? "Excelent!"
                         : finalScore >= 50 ? "Bine!"
                         : "Continuă să exersezi!";

            WinEmoji.Text    = emoji;
            WinTitle.Text    = title;
            WinSubtitle.Text = $"{_correct} corecte  •  {_wrong} greșite  •  Seria maximă: {_bestStreak}";
            WinScore.Text    = $"Scor final: {finalScore:F1} / 100";

            WinOverlay.Visibility = Visibility.Visible;

            _ = SaveToFirebaseAsync(finalScore);
        }

        private async System.Threading.Tasks.Task SaveToFirebaseAsync(double finalScore)
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                TimeSpan elapsed = DateTime.Now - _startTime;
                string duration  = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";

                var payload = new
                {
                    game       = "Matematică rapidă",
                    dateTime   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration   = duration,
                    difficulty = _difficultyLabel,
                    scor       = Math.Round(finalScore, 2)
                };

                string json   = JsonSerializer.Serialize(payload);
                var content   = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync($"{_dbUrl}/{uid}/activities.json?auth={token}", content);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  NAV din overlay
        // ═══════════════════════════════════════════════════
        private void WinPlayAgain_Click(object s, RoutedEventArgs e)
        {
            WinOverlay.Visibility      = Visibility.Collapsed;
            PanelGame.Visibility       = Visibility.Collapsed;
            PanelDifficulty.Visibility = Visibility.Visible;
        }

        private void BtnBack_Click(object s, RoutedEventArgs e)
        {
            _timer.Stop();
            if (Owner is Dashboard dash)
            {
                dash.Show();
                if (!_gameActive || _correct + _wrong > 0)
                    _ = dash.LoadActivitiesFromFirebaseAsync();
            }
            Close();
        }

        private void Window_Closing(object s, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            if (Owner is Dashboard dash && !dash.IsVisible) dash.Show();
        }

        private string GetReg(string key)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RegPath);
                return k?.GetValue(key)?.ToString() ?? "";
            }
            catch { return ""; }
        }
    }
}