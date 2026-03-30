using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class StroopGame : Window
    {
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private const string RegPath = @"Software\FocusAI";
        private readonly bool _isDark;
        private const int TotalRounds = 30;

        private static readonly (string Key, string Hex, string RomanianName)[] Colors =
        {
            ("Roșu",       "#EF4444", "ROȘU"),
            ("Verde",      "#22C55E", "VERDE"),
            ("Albastru",   "#3B82F6", "ALBASTRU"),
            ("Galben",     "#EAB308", "GALBEN"),
            ("Portocaliu", "#F97316", "PORTOCALIU"),
            ("Violet",     "#A855F7", "VIOLET"),
        };

        private int _numColors;
        private int _numOptions;
        private double _timeLimitSeconds;

        private int _currentRound    = 0;
        private int _score           = 0;
        private int _correctCount    = 0;
        private int _wrongCount      = 0;
        private int _streak          = 0;
        private int _maxStreak       = 0;
        private bool _waitingForNext = false;
        private string _correctColorKey  = "";
        private string _wordColorKey     = "";   // culoarea REALĂ a cuvântului scris
        private string _difficulty       = "Ușor";

        private readonly List<double> _responseTimes = new();
        private readonly Stopwatch _roundStopwatch   = new();
        private readonly Stopwatch _gameStopwatch    = new();

        private DispatcherTimer? _limitTimer;
        private double _timeLeft;

        private Button[] _btns = Array.Empty<Button>();
        private readonly Random _rng = new();
        private bool _closedByButton = false;

        // Păstrăm lista de chei a opțiunilor afișate pe butoane (pentru Tag)
        private List<string> _currentOptionKeys = new();

        public StroopGame(bool isDark)
        {
            InitializeComponent();
            _isDark = isDark;
            ThemeManager.Apply(_isDark);
            _btns = new[] { Btn0, Btn1, Btn2, Btn3 };
            Closing += StroopGame_Closing;
        }

        private void StroopGame_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _limitTimer?.Stop();
            if (!_closedByButton && Owner is Dashboard db) db.Show();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (DiffEasy.IsChecked == true)
            {
                _difficulty       = "Ușor";
                _numColors        = 4;
                _numOptions       = 4;
                _timeLimitSeconds = 0;
            }
            else if (DiffMedium.IsChecked == true)
            {
                _difficulty       = "Mediu";
                _numColors        = 5;
                _numOptions       = 4;
                _timeLimitSeconds = 5.0;
            }
            else
            {
                _difficulty       = "Dificil";
                _numColors        = 6;
                _numOptions       = 4;
                _timeLimitSeconds = 3.5;
            }

            TxtDifficulty.Text = _difficulty;
            ResetState();

            WinOverlay.Visibility = Visibility.Collapsed;

            ShowScreen(ScreenGame);
            NextRound();
        }

        private void ResetState()
        {
            _currentRound   = 0;
            _score          = 0;
            _correctCount   = 0;
            _wrongCount     = 0;
            _streak         = 0;
            _maxStreak      = 0;
            _waitingForNext = false;
            _responseTimes.Clear();
            _gameStopwatch.Restart();
            UpdateScoreUI();
        }

        private void NextRound()
        {
            if (_currentRound >= TotalRounds)
            {
                EndGame();
                return;
            }

            _waitingForNext = false;
            _currentRound++;
            UpdateProgress();

            var activeColors = Colors.Take(_numColors).ToArray();

            // Culoarea cernelii (răspunsul corect)
            var inkColor  = activeColors[_rng.Next(activeColors.Length)];
            // Cuvântul afișat — diferit de culoarea cernelii
            var wordCandidates = activeColors.Where(c => c.Key != inkColor.Key).ToArray();
            var wordColor = wordCandidates[_rng.Next(wordCandidates.Length)];

            _correctColorKey = inkColor.Key;
            _wordColorKey    = wordColor.Key;   // culoarea reală a cuvântului scris

            WordLabel.Text       = wordColor.RomanianName;
            WordLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(inkColor.Hex));

            // ── Construim lista de 4 opțiuni ──────────────────────────────────────
            // Slot 0: răspunsul corect (culoarea cernelii)
            // Slot 1: cuvântul scris cu propria lui culoare (distractor semantic obligatoriu)
            // Slot 2-3: alte culori aleatorii diferite de primele două
            var optionKeys = new List<string> { inkColor.Key, wordColor.Key };

            var remaining = activeColors
                .Where(c => c.Key != inkColor.Key && c.Key != wordColor.Key)
                .OrderBy(_ => _rng.Next())
                .Take(_numOptions - 2)
                .Select(c => c.Key)
                .ToList();
            optionKeys.AddRange(remaining);

            // Completăm dacă nu avem destule culori distincte
            while (optionKeys.Count < _numOptions)
            {
                var extra = activeColors[_rng.Next(activeColors.Length)].Key;
                if (!optionKeys.Contains(extra)) optionKeys.Add(extra);
            }

            // Amestecăm pozițiile
            optionKeys = optionKeys.OrderBy(_ => _rng.Next()).ToList();
            _currentOptionKeys = optionKeys;

            // ── Stilizăm butoanele ──────────────────────────────────────────────
            for (int i = 0; i < _btns.Length; i++)
            {
                string key  = optionKeys[i];
                var cInfo   = Colors.FirstOrDefault(c => c.Key == key);
                if (cInfo == default) cInfo = activeColors[0];

                var btnColor = (Color)ColorConverter.ConvertFromString(cInfo.Hex);
                var bgBrush  = new SolidColorBrush(Color.FromArgb(
                    _isDark ? (byte)40 : (byte)25,
                    btnColor.R, btnColor.G, btnColor.B));

                _btns[i].Background  = bgBrush;
                _btns[i].BorderBrush = new SolidColorBrush(btnColor);
                _btns[i].Foreground  = new SolidColorBrush(btnColor);
                _btns[i].Content     = cInfo.RomanianName;
                _btns[i].Tag         = key;
                _btns[i].IsEnabled   = true;
                _btns[i].Opacity     = 1.0;
            }

            _roundStopwatch.Restart();
            StartLimitTimer();
        }

        private void StartLimitTimer()
        {
            _limitTimer?.Stop();
            if (_timeLimitSeconds <= 0)
            {
                TxtTimer.Text = "∞";
                return;
            }

            _timeLeft     = _timeLimitSeconds;
            TxtTimer.Text = _timeLeft.ToString("F1");
            TxtTimer.Foreground = (SolidColorBrush)FindResource("TxtPrimary");

            _limitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _limitTimer.Tick += LimitTimer_Tick;
            _limitTimer.Start();
        }

        private void LimitTimer_Tick(object? sender, EventArgs e)
        {
            _timeLeft -= 0.1;
            TxtTimer.Text = Math.Max(0, _timeLeft).ToString("F1");
            if (_timeLeft < 1.5)
                TxtTimer.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            if (_timeLeft <= 0)
            {
                _limitTimer?.Stop();
                if (!_waitingForNext) HandleAnswer(null);
            }
        }

        private void Answer_Click(object sender, RoutedEventArgs e)
        {
            if (_waitingForNext) return;
            var btn    = (Button)sender;
            string chosen = btn.Tag?.ToString() ?? "";
            HandleAnswer(chosen);
        }

        private void HandleAnswer(string? chosen)
        {
            _limitTimer?.Stop();
            _waitingForNext = true;
            double elapsed  = _roundStopwatch.Elapsed.TotalSeconds;
            _responseTimes.Add(elapsed);
            bool correct = chosen == _correctColorKey;

            if (correct)
            {
                _correctCount++;
                _streak++;
                if (_streak > _maxStreak) _maxStreak = _streak;
                double timeBonus = _timeLimitSeconds > 0
                    ? Math.Max(0, 1.0 - (elapsed / _timeLimitSeconds))
                    : Math.Max(0, 1.0 - (elapsed / 3.0));
                int pts = (int)(50 + 50 * timeBonus);
                if (_streak >= 3) pts = (int)(pts * 1.2);
                _score += pts;
            }
            else
            {
                _wrongCount++;
                _streak = 0;
            }

            // Actualizăm badge-ul streak
            if (_streak >= 3)
            {
                StreakBadge.Visibility = Visibility.Visible;
                TxtStreak.Text = $"🔥 {_streak} la rând!";
            }
            else
            {
                StreakBadge.Visibility = Visibility.Collapsed;
            }

            HighlightButtons(chosen);
            UpdateScoreUI();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) => { timer.Stop(); NextRound(); };
            timer.Start();
        }

        private void HighlightButtons(string? chosen)
        {
            foreach (var btn in _btns)
            {
                btn.IsEnabled = false;
                string key = btn.Tag?.ToString() ?? "";
                if (key == _correctColorKey)
                {
                    btn.Background  = new SolidColorBrush(Color.FromArgb(80, 34, 197, 94));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                }
                else if (chosen != null && key == chosen)
                {
                    btn.Background  = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
                else
                {
                    btn.Opacity = 0.35;
                }
            }
        }

        private void UpdateScoreUI()
        {
            TxtScore.Text   = _score.ToString();
            TxtCorrect.Text = $"{_correctCount} corecte";
            TxtWrong.Text   = $"{_wrongCount} greșite";
        }

        private void UpdateProgress()
        {
            TxtProgress.Text = $"Runda {_currentRound} / {TotalRounds}";
            Dispatcher.InvokeAsync(() =>
            {
                double parentW    = ((Border)ProgressBar.Parent).ActualWidth;
                ProgressBar.Width = parentW * (_currentRound - 1) / TotalRounds;
            }, DispatcherPriority.Loaded);
        }

        // ═══════════════════════════════════════════════════
        //  END GAME — overlay identic cu MemorieGame
        // ═══════════════════════════════════════════════════
        private void EndGame()
        {
            _limitTimer?.Stop();
            _gameStopwatch.Stop();

            double accuracy   = TotalRounds > 0 ? _correctCount / (double)TotalRounds * 100.0 : 0;
            double avgTime    = _responseTimes.Count > 0 ? _responseTimes.Average() : 0;
            double finalScore = Math.Min(100.0, _score / (double)(TotalRounds * 100) * 100.0);
            TimeSpan duration = _gameStopwatch.Elapsed;

            string emoji = finalScore >= 85 ? "🏆"
                         : finalScore >= 60 ? "🥈"
                         : "💪";
            string title = finalScore >= 85 ? "Excelent!"
                         : finalScore >= 60 ? "Bine făcut!"
                         : "Nu te descuraja!";

            WinEmoji.Text    = emoji;
            WinTitle.Text    = title;
            WinSubtitle.Text = $"{_correctCount} corecte  •  Acuratețe: {accuracy:F1}%  •  Timp mediu: {avgTime:F2}s";
            WinScore.Text    = $"Scor final: {finalScore:F1} / 100";

            WinOverlay.Visibility = Visibility.Visible;

            _ = SaveActivityAsync(finalScore, duration);
        }

        private async System.Threading.Tasks.Task SaveActivityAsync(double score, TimeSpan duration)
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                string durationStr = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
                var payload = new
                {
                    game       = "Stroop Test",
                    dateTime   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration   = durationStr,
                    difficulty = _difficulty,
                    scor       = Math.Round(score, 2),
                    correct    = _correctCount,
                    wrong      = _wrongCount,
                    streak     = _maxStreak
                };

                string json  = JsonSerializer.Serialize(payload);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                string url   = $"{_dbUrl}/{uid}/activities.json?auth={token}";
                await _http.PostAsync(url, content);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  NAV
        // ═══════════════════════════════════════════════════
        private void WinPlayAgain_Click(object sender, RoutedEventArgs e)
        {
            WinOverlay.Visibility = Visibility.Collapsed;
            ShowScreen(ScreenIntro);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _limitTimer?.Stop();
            _closedByButton = true;
            if (Owner is Dashboard db) db.Show();
            Close();
        }

        private void ShowScreen(Border screen)
        {
            ScreenIntro.Visibility  = Visibility.Collapsed;
            ScreenGame.Visibility   = Visibility.Collapsed;
            screen.Visibility       = Visibility.Visible;
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

        // BtnBack_Click din header (diferit de BackButton în overlay)
        private void BtnBack_Click(object sender, RoutedEventArgs e) => BackButton_Click(sender, e);
    }
}