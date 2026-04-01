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
    public partial class VisualSearchGame : Window
    {
        private const string RegPath = @"Software\FocusAI";
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private const int TotalRounds = 8;

        private int _round       = 0;
        private int _score       = 0;
        private int _lives       = 3;
        private int _timeLeft    = 20;
        private int _targetIndex = -1;
        private bool _isPlaying  = false;
        private bool _isDark;
        private bool _closedByButton = false;
        private string _difficulty   = "Mediu";

        private DispatcherTimer _timer = new();
        private DateTime _roundStart;
        private double _timerBarMaxWidth = 0;
        private readonly Stopwatch _gameStopwatch = new();

        private static readonly (string Distractor, string Odd)[] _charSets =
        {
            ("S","5"), ("O","0"), ("B","8"), ("G","6"), ("Z","2"),
            ("E","3"), ("I","1"), ("A","4"), ("P","R"), ("E","F"),
            ("C","G"), ("V","U"), ("W","M"), ("H","N"), ("n","h"),
            ("d","b"), ("q","p"), ("a","d"), ("m","w"), ("K","R"),
            ("D","B"), ("T","Y"), ("L","J"), ("X","K"), ("f","t"),
            ("c","e"), ("o","c"), ("i","j"), ("6","9"), ("1","7"),
        };

        private (int Cols, int Rows) GetGridSize() => _round switch
        {
            1 or 2 => (10, 6),
            3 or 4 => (14, 8),
            5 or 6 => (18, 10),
            _      => (22, 12),
        };

        private string _distractorChar = "";
        private string _oddChar        = "";
        private readonly Random _rng   = new();
        private int _lastSetIndex      = -1;

        private Color TextColor    => _isDark
            ? (Color)ColorConverter.ConvertFromString("#E2E8F0")
            : (Color)ColorConverter.ConvertFromString("#0F172A");
        private Color CellBgColor  => _isDark
            ? (Color)ColorConverter.ConvertFromString("#1E293B")
            : (Color)ColorConverter.ConvertFromString("#F8FAFC");

        public VisualSearchGame(bool isDark)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            Loaded  += (_, _) => ReadTimerBarWidth();
            Closing += Window_Closing;
        }

        private void ReadTimerBarWidth()
        {
            if (TimerBar.Parent is Border b && b.ActualWidth > 0)
                _timerBarMaxWidth = b.ActualWidth;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _difficulty = RbEasy.IsChecked == true ? "Ușor"
                        : RbHard.IsChecked == true ? "Dificil"
                        : "Mediu";
            _round  = 0;
            _score  = 0;
            _lives  = 3;
            _isPlaying = true;
            _gameStopwatch.Restart();

            // Ascunde overlay-ul dacă era vizibil
            WinOverlay.Visibility = Visibility.Collapsed;

            PanelInstruction.Visibility = Visibility.Collapsed;
            PanelGame.Visibility        = Visibility.Visible;

            NextRound();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            // Buton "Joacă din nou" din overlay — duce la ecranul de instrucțiuni
            WinOverlay.Visibility = Visibility.Collapsed;
            PanelGame.Visibility  = Visibility.Collapsed;
            PanelInstruction.Visibility = Visibility.Visible;
        }

        private void NextRound()
        {
            if (_round >= TotalRounds) { EndGame(true); return; }

            _timer.Stop();
            _round++;

            _timeLeft = _difficulty switch { "Ușor" => 25, "Dificil" => 15, _ => 20 };

            int idx;
            do { idx = _rng.Next(_charSets.Length); } while (idx == _lastSetIndex);
            _lastSetIndex   = idx;
            _distractorChar = _charSets[idx].Distractor;
            _oddChar        = _charSets[idx].Odd;

            var (cols, rows) = GetGridSize();
            int total        = cols * rows;
            _targetIndex     = _rng.Next(total);

            TxtRound.Text    = $"{_round}/{TotalRounds}";
            TxtScore.Text    = _score.ToString();
            TxtLives.Text    = string.Concat(Enumerable.Repeat("❤️", _lives))
                             + string.Concat(Enumerable.Repeat("🖤", 3 - _lives));
            TxtTimer.Text    = _timeLeft.ToString();
            TxtTimer.Foreground = (SolidColorBrush)FindResource("TxtPrimary");
            TxtRoundInfo.Text   = $"Runda {_round}/{TotalRounds}  •  {cols}×{rows}";

            GameGrid.Columns = cols;
            GameGrid.Children.Clear();

            int fs    = cols <= 10 ? 28 : cols <= 14 ? 22 : cols <= 18 ? 17 : 14;
            int cellW = cols <= 10 ? 62 : cols <= 14 ? 46 : cols <= 18 ? 36 : 30;

            var textBrush   = new SolidColorBrush(TextColor);
            var cellBgBrush = new SolidColorBrush(CellBgColor);

            for (int i = 0; i < total; i++)
            {
                bool isTarget = i == _targetIndex;
                string ch     = isTarget ? _oddChar : _distractorChar;

                var btn = new Button
                {
                    Width     = cellW,
                    Height    = cellW,
                    Margin    = new Thickness(2),
                    Tag       = isTarget,
                    Background = cellBgBrush,
                    BorderThickness = new Thickness(0),
                    Cursor    = System.Windows.Input.Cursors.Hand,
                    FontSize  = fs,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontWeight = FontWeights.Bold,
                    Content    = ch,
                    Foreground = textBrush,
                };

                var tpl = new ControlTemplate(typeof(Button));
                var fef = new FrameworkElementFactory(typeof(Border));
                fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                fef.SetBinding(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                    });
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
                fef.AppendChild(cp);
                tpl.VisualTree = fef;
                btn.Template = tpl;

                btn.Click += Cell_Click;
                GameGrid.Children.Add(btn);
            }

            Dispatcher.InvokeAsync(() => { ReadTimerBarWidth(); UpdateTimerBar(); }, DispatcherPriority.Loaded);
            _roundStart = DateTime.Now;
            _timer.Start();
        }

        private void Cell_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlaying) return;
            var btn       = (Button)sender;
            bool isTarget = (bool)btn.Tag;

            if (isTarget)
            {
                _timer.Stop();
                double elapsed = (DateTime.Now - _roundStart).TotalSeconds;
                int timeBonus  = (int)Math.Max(0, _timeLeft * 15);
                int pts        = 200 + timeBonus + (_round * 30);
                _score += pts;

                btn.Background = new SolidColorBrush(Color.FromArgb(200, 34, 197, 94));
                ShowFeedback("✅", $"+{pts} puncte!", $"Runda {_round} completată în {elapsed:F1}s");

                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
                t.Tick += (_, _) => { t.Stop(); PanelFeedback.Visibility = Visibility.Collapsed; NextRound(); };
                t.Start();
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(160, 239, 68, 68));
                var restore    = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                restore.Tick += (_, _) => { restore.Stop(); btn.Background = new SolidColorBrush(CellBgColor); };
                restore.Start();

                _lives--;
                TxtLives.Text = string.Concat(Enumerable.Repeat("❤️", Math.Max(_lives, 0)))
                              + string.Concat(Enumerable.Repeat("🖤", 3 - Math.Max(_lives, 0)));

                if (_lives <= 0)
                {
                    _timer.Stop();
                    EndGame(false);
                }
                else
                {
                    ShowFeedback("❌", "Greșit!", $"Mai ai {_lives} {(_lives == 1 ? "viață" : "vieți")}");
                    var ft = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                    ft.Tick += (_, _) => { ft.Stop(); PanelFeedback.Visibility = Visibility.Collapsed; };
                    ft.Start();
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timeLeft--;
            TxtTimer.Text = _timeLeft.ToString();
            UpdateTimerBar();

            TxtTimer.Foreground = _timeLeft <= 5
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                : _timeLeft <= 10
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
                    : (SolidColorBrush)FindResource("TxtPrimary");

            if (_timeLeft <= 0)
            {
                _timer.Stop();
                _lives--;
                TxtLives.Text = string.Concat(Enumerable.Repeat("❤️", Math.Max(_lives, 0)))
                              + string.Concat(Enumerable.Repeat("🖤", 3 - Math.Max(_lives, 0)));
                HighlightTarget();

                if (_lives <= 0) { EndGame(false); return; }

                ShowFeedback("⏰", "Timp expirat!", $"Mai ai {_lives} {(_lives == 1 ? "viață" : "vieți")}");
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
                t.Tick += (_, _) => { t.Stop(); PanelFeedback.Visibility = Visibility.Collapsed; NextRound(); };
                t.Start();
            }
        }

        private void HighlightTarget()
        {
            if (_targetIndex >= 0 && _targetIndex < GameGrid.Children.Count
                && GameGrid.Children[_targetIndex] is Button b)
                b.Background = new SolidColorBrush(Color.FromArgb(220, 234, 179, 8));
        }

        private void UpdateTimerBar()
        {
            if (TimerBar.Parent is Border pb && pb.ActualWidth > 0)
                _timerBarMaxWidth = pb.ActualWidth;
            if (_timerBarMaxWidth <= 0) return;

            int maxTime   = _difficulty switch { "Ușor" => 25, "Dificil" => 15, _ => 20 };
            double ratio  = Math.Max(0, (double)_timeLeft / maxTime);
            TimerBar.Width = _timerBarMaxWidth * ratio;
            TimerBar.Background = ratio > 0.5
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"))
                : ratio > 0.25
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        }

        // ═══════════════════════════════════════════════════
        //  END GAME — overlay ca la MemorieGame
        // ═══════════════════════════════════════════════════
        private void EndGame(bool won)
        {
            _isPlaying = false;
            _timer.Stop();
            _gameStopwatch.Stop();

            PanelFeedback.Visibility = Visibility.Collapsed;

            TimeSpan duration  = _gameStopwatch.Elapsed;
            double normalized  = Math.Min(100.0, _score / (double)(TotalRounds * 575) * 100.0);

            string emoji = won              ? "🏆"
                         : _round > TotalRounds / 2 ? "😤"
                         : "💀";
            string title = won ? "Felicitări!" : "Game Over";

            WinEmoji.Text    = emoji;
            WinTitle.Text    = title;
            WinSubtitle.Text = won
                ? $"Ai completat toate {TotalRounds} rundele!"
                : $"Ai ajuns la runda {_round}/{TotalRounds}  •  Dificultate: {_difficulty}";
            WinScore.Text = $"Scor: {_score}  •  {duration.Minutes:D2}:{duration.Seconds:D2}";

            // Arată overlay-ul pe PanelGame
            WinOverlay.Visibility = Visibility.Visible;

            _ = SaveActivityAsync(won, duration);
        }

        private void ShowFeedback(string icon, string msg, string sub)
        {
            TxtFeedbackIcon.Text = icon;
            TxtFeedbackMsg.Text  = msg;
            TxtFeedbackSub.Text  = sub;
            PanelFeedback.Visibility = Visibility.Visible;
        }

        private async System.Threading.Tasks.Task SaveActivityAsync(bool won, TimeSpan duration)
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                double normalized  = Math.Min(100.0, _score / (double)(TotalRounds * 575) * 100.0);
                string durationStr = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";

                var payload = new
                {
                    game        = "Visual Search",
                    difficulty  = _difficulty,
                    scor        = Math.Round(normalized, 2),
                    rawScore    = _score,
                    round       = _round,
                    totalRounds = TotalRounds,
                    dateTime    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration    = durationStr,
                    completed   = won
                };

                string json  = JsonSerializer.Serialize(payload);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync($"{_dbUrl}/{uid}/activities.json?auth={token}", content);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  NAV
        // ═══════════════════════════════════════════════════
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isPlaying       = false;
            _closedByButton  = true;
            Owner?.Show();
            Close();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            if (!_closedByButton) Owner?.Show();
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

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            _closedByButton = true;
            if (Owner is Dashboard db) db.Show();
            Close();
        }
    }
}