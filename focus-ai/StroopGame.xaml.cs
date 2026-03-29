using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class StroopGame : Window
    {
        // ── Firebase / Registry ──
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private const string RegPath = @"Software\FocusAI";

        // ── Theme ──
        private readonly bool _isDark;

        // ── Game Config ──
        private const int TotalRounds = 30;

        // Culorile jocului: (Cheie, Hex afișat)
        private static readonly (string Key, string Hex, string RomanianName)[] Colors =
        {
            ("Roșu",    "#EF4444", "ROȘU"),
            ("Verde",   "#22C55E", "VERDE"),
            ("Albastru","#3B82F6", "ALBASTRU"),
            ("Galben",  "#EAB308", "GALBEN"),
            ("Portocaliu","#F97316","PORTOCALIU"),
            ("Violet",  "#A855F7", "VIOLET"),
        };

        // Per dificultate: câte culori folosim și câte opțiuni afișăm
        private int _numColors;
        private int _numOptions;
        private double _timeLimitSeconds; // 0 = fără limită

        // ── Game State ──
        private int    _currentRound   = 0;
        private int    _score          = 0;
        private int    _correctCount   = 0;
        private int    _wrongCount     = 0;
        private int    _streak         = 0;
        private int    _maxStreak      = 0;
        private bool   _waitingForNext = false;
        private string _correctColorKey = "";
        private string _difficulty      = "Ușor";

        private readonly List<double> _responseTimes = new();
        private readonly Stopwatch    _roundStopwatch = new();
        private readonly Stopwatch    _gameStopwatch  = new();

        // ── Timer pentru time limit (Dificil) ──
        private DispatcherTimer? _limitTimer;
        private double           _timeLeft;

        // ── Buttons array ──
        private Button[] _btns = Array.Empty<Button>();

        // ── Random ──
        private readonly Random _rng = new();

        // ─────────────────────────────────────────────
        public StroopGame(bool isDark)
        {
            InitializeComponent();
            _isDark = isDark;
            ThemeManager.Apply(_isDark);
            _btns = new[] { Btn0, Btn1, Btn2, Btn3 };
        }

        // ═══════════════════════════════════════════════
        //  START
        // ═══════════════════════════════════════════════
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (DiffEasy.IsChecked == true)
            {
                _difficulty        = "Ușor";
                _numColors         = 4;
                _numOptions        = 4;
                _timeLimitSeconds  = 0;
            }
            else if (DiffMedium.IsChecked == true)
            {
                _difficulty        = "Mediu";
                _numColors         = 5;
                _numOptions        = 4;
                _timeLimitSeconds  = 5.0;
            }
            else
            {
                _difficulty        = "Dificil";
                _numColors         = 6;
                _numOptions        = 4;
                _timeLimitSeconds  = 3.5;
            }

            TxtDifficulty.Text = _difficulty;
            ResetState();
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

        // ═══════════════════════════════════════════════
        //  ROUND LOGIC
        // ═══════════════════════════════════════════════
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

            // Alege culorile active (subset din Colors)
            var activeColors = Colors.Take(_numColors).ToArray();

            // Cuvântul scris (text)
            var wordColor = activeColors[_rng.Next(activeColors.Length)];
            // Culoarea cernelii (poate fi diferită – efect Stroop)
            var inkColor  = activeColors[_rng.Next(activeColors.Length)];

            // Pe Ușor: ~40% congruent, pe Mediu/Dificil ~20%
            double congruentChance = _difficulty == "Ușor" ? 0.40 : 0.20;
            if (_rng.NextDouble() < congruentChance)
                inkColor = wordColor;

            _correctColorKey = inkColor.Key;

            // Afișează cuvântul colorat
            WordLabel.Text       = wordColor.RomanianName;
            WordLabel.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(inkColor.Hex));

            // Construiește opțiunile: corect + N-1 distractori unici
            var optionKeys = new List<string> { inkColor.Key };
            var others = activeColors.Where(c => c.Key != inkColor.Key)
                                     .OrderBy(_ => _rng.Next())
                                     .Take(_numOptions - 1)
                                     .Select(c => c.Key)
                                     .ToList();
            optionKeys.AddRange(others);
            optionKeys = optionKeys.OrderBy(_ => _rng.Next()).ToList(); // shuffle

            // Dacă avem 4 opțiuni dar mai puțin de 4 culori disponibile, completăm cu culori random
            while (optionKeys.Count < _numOptions)
                optionKeys.Add(activeColors[_rng.Next(activeColors.Length)].Key);

            // Setează butoanele
            for (int i = 0; i < _btns.Length; i++)
            {
                string key   = i < optionKeys.Count ? optionKeys[i] : activeColors[i % activeColors.Length].Key;
                var    cInfo = Colors.FirstOrDefault(c => c.Key == key);
                if (cInfo == default) cInfo = activeColors[0];

                var hex      = cInfo.Hex;
                var btnColor = (Color)ColorConverter.ConvertFromString(hex);
                var bgBrush  = new SolidColorBrush(Color.FromArgb(
                    _isDark ? (byte)40 : (byte)25,
                    btnColor.R, btnColor.G, btnColor.B));
                var borderBrush = new SolidColorBrush(btnColor);

                _btns[i].Background   = bgBrush;
                _btns[i].BorderBrush  = borderBrush;
                _btns[i].Foreground   = new SolidColorBrush(btnColor);
                _btns[i].Content      = cInfo.RomanianName;
                _btns[i].Tag          = key;
                _btns[i].IsEnabled    = true;
                _btns[i].Opacity      = 1.0;
            }

            // Start timer per rundă
            _roundStopwatch.Restart();
            StartLimitTimer();
        }

        // ═══════════════════════════════════════════════
        //  LIMIT TIMER (Mediu / Dificil)
        // ═══════════════════════════════════════════════
        private void StartLimitTimer()
        {
            _limitTimer?.Stop();
            if (_timeLimitSeconds <= 0) return;

            _timeLeft = _timeLimitSeconds;
            TxtTimer.Text       = _timeLeft.ToString("F1");
            TxtTimer.Foreground = (SolidColorBrush)FindResource("TxtPrimary");

            _limitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _limitTimer.Tick += LimitTimer_Tick;
            _limitTimer.Start();
        }

        private void LimitTimer_Tick(object? sender, EventArgs e)
        {
            _timeLeft -= 0.1;
            TxtTimer.Text = Math.Max(0, _timeLeft).ToString("F1");

            // Colorează roșu când < 1.5s
            if (_timeLeft < 1.5)
                TxtTimer.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));

            if (_timeLeft <= 0)
            {
                _limitTimer?.Stop();
                if (!_waitingForNext)
                    HandleAnswer(null); // time out = greșit
            }
        }

        // ═══════════════════════════════════════════════
        //  ANSWER
        // ═══════════════════════════════════════════════
        private void Answer_Click(object sender, RoutedEventArgs e)
        {
            if (_waitingForNext) return;
            var btn = (Button)sender;
            string chosen = btn.Tag?.ToString() ?? "";
            HandleAnswer(chosen);
        }

        private void HandleAnswer(string? chosen)
        {
            _limitTimer?.Stop();
            _waitingForNext = true;

            double elapsed = _roundStopwatch.Elapsed.TotalSeconds;
            _responseTimes.Add(elapsed);

            bool correct = chosen == _correctColorKey;

            // Calculează punctaj pentru runda aceasta
            if (correct)
            {
                _correctCount++;
                _streak++;
                if (_streak > _maxStreak) _maxStreak = _streak;

                // Punctaj bazat pe viteză:
                // Max 100/round dacă răspunzi instantaneu, scade cu timpul
                double timeBonus = _timeLimitSeconds > 0
                    ? Math.Max(0, 1.0 - (elapsed / _timeLimitSeconds))
                    : Math.Max(0, 1.0 - (elapsed / 3.0));
                int pts = (int)(50 + 50 * timeBonus);
                if (_streak >= 3) pts = (int)(pts * 1.2); // streak bonus
                _score += pts;

                ShowFeedback(true);
            }
            else
            {
                _wrongCount++;
                _streak = 0;
                ShowFeedback(false);
            }

            // Highlight butoane
            HighlightButtons(chosen);
            UpdateScoreUI();

            // Treci la runda următoare după 600ms
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
                    // Verde = corect
                    btn.Background  = new SolidColorBrush(
                        Color.FromArgb(80, 34, 197, 94));
                    btn.BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#22C55E"));
                }
                else if (chosen != null && key == chosen)
                {
                    // Roșu = ales greșit
                    btn.Background  = new SolidColorBrush(
                        Color.FromArgb(80, 239, 68, 68));
                    btn.BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#EF4444"));
                }
                else
                {
                    btn.Opacity = 0.35;
                }
            }
        }

        private void ShowFeedback(bool correct)
        {
            FeedbackIcon.Text    = correct ? "✅" : "❌";
            FeedbackIcon.Opacity = 0.9;

            // Streak badge
            if (_streak >= 3)
            {
                StreakBadge.Visibility = Visibility.Visible;
                TxtStreak.Text         = $"🔥 {_streak} la rând!";
            }
            else
            {
                StreakBadge.Visibility = Visibility.Collapsed;
            }

            var fade = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            fade.Tick += (_, _) => { fade.Stop(); FeedbackIcon.Opacity = 0; };
            fade.Start();
        }

        private void UpdateScoreUI()
        {
            TxtScore.Text   = _score.ToString();
            TxtCorrect.Text = $"{_correctCount} corecte";
            TxtWrong.Text   = $"{_wrongCount} greșite";
        }

        private void UpdateProgress()
        {
            TxtProgress.Text = $"Întrebarea {_currentRound} / {TotalRounds}";

            // Lățimea barei de progres (trebuie să calculăm relativ la părinte)
            // Folosim un dispatcher delayed pentru a citi ActualWidth-ul după render
            Dispatcher.InvokeAsync(() =>
            {
                double parentW = ((Border)ProgressBar.Parent).ActualWidth;
                ProgressBar.Width = parentW * (_currentRound - 1) / TotalRounds;
            }, DispatcherPriority.Loaded);
        }

        // ═══════════════════════════════════════════════
        //  END GAME
        // ═══════════════════════════════════════════════
        private void EndGame()
        {
            _limitTimer?.Stop();
            _gameStopwatch.Stop();

            double finalScore = Math.Min(100.0, _score / (double)(TotalRounds * 100) * 100.0);
            double accuracy   = TotalRounds > 0
                ? _correctCount / (double)TotalRounds * 100.0
                : 0;
            double avgTime    = _responseTimes.Count > 0
                ? _responseTimes.Average()
                : 0;
            TimeSpan duration = _gameStopwatch.Elapsed;

            // Populează ecranul de rezultate
            BigScore.Text     = finalScore.ToString("F2");
            ResCorrect.Text   = _correctCount.ToString();
            ResWrong.Text     = _wrongCount.ToString();
            ResAvgTime.Text   = $"{avgTime:F2}s";
            ResStreak.Text    = _maxStreak.ToString();
            ResAccuracy.Text  = $"{accuracy:F1}%";

            // Bara acuratețe
            Dispatcher.InvokeAsync(() =>
            {
                double parentW = ((Border)AccuracyBar.Parent).ActualWidth;
                AccuracyBar.Width = parentW * accuracy / 100.0;
                AccuracyBar.Background = accuracy >= 80
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"))
                    : accuracy >= 50
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FB923C"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }, DispatcherPriority.Loaded);

            // Trophy / mesaj
            if (finalScore >= 85)
            {
                TrophyIcon.Text    = "🏆";
                ResultTitle.Text   = "Excelent!";
                ResultSubtitle.Text = "Performanță remarcabilă la Stroop Test!";
            }
            else if (finalScore >= 60)
            {
                TrophyIcon.Text    = "🥈";
                ResultTitle.Text   = "Bine făcut!";
                ResultSubtitle.Text = "Continuă să exersezi pentru un scor mai mare!";
            }
            else
            {
                TrophyIcon.Text    = "💪";
                ResultTitle.Text   = "Nu te descuraja!";
                ResultSubtitle.Text = "Practica duce la perfecțiune. Încearcă din nou!";
            }

            ShowScreen(ScreenResults);

            // Salvare Firebase
            TxtSaving.Visibility = Visibility.Visible;
            _ = SaveActivityAsync(finalScore, duration);
        }

        // ═══════════════════════════════════════════════
        //  FIREBASE SAVE
        // ═══════════════════════════════════════════════
        private async Task SaveActivityAsync(double score, TimeSpan duration)
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                string durationStr = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
                string dateTimeStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                var payload = new
                {
                    dateTime   = dateTimeStr,
                    duration   = durationStr,
                    game       = "Stroop Test",
                    difficulty = _difficulty,
                    scor       = Math.Round(score, 2),
                    correct    = _correctCount,
                    wrong      = _wrongCount,
                    streak     = _maxStreak
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{_dbUrl}/{uid}/activities.json?auth={token}";
                await _http.PostAsync(url, content);

                Dispatcher.Invoke(() =>
                    TxtSaving.Text = "✅ Activitate salvată cu succes!");
            }
            catch
            {
                Dispatcher.Invoke(() =>
                    TxtSaving.Text = "⚠️ Nu s-a putut salva activitatea.");
            }
        }

        // ═══════════════════════════════════════════════
        //  NAVIGATION
        // ═══════════════════════════════════════════════
        private void BtnPlayAgain_Click(object sender, RoutedEventArgs e)
        {
            ShowScreen(ScreenIntro);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _limitTimer?.Stop();
            if (Owner is Dashboard db)
            {
                db.Show();
                // Refresh activitățile în dashboard
                db.Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(300);
                    // Dacă tab-ul activități e vizibil, reîncarcă
                });
            }
            Close();
        }

        // ═══════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════
        private void ShowScreen(Border screen)
        {
            ScreenIntro.Visibility   = Visibility.Collapsed;
            ScreenGame.Visibility    = Visibility.Collapsed;
            ScreenResults.Visibility = Visibility.Collapsed;
            screen.Visibility        = Visibility.Visible;
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