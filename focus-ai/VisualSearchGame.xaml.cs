using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class VisualSearchGame : Window
    {
        // ── Config ──
        private const string RegPath = @"Software\FocusAI";
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();

        // ── Game state ──
        private int _level      = 1;
        private int _score      = 0;
        private int _lives      = 3;
        private int _timeLeft   = 30;
        private int _targetIndex = -1;
        private bool _isPlaying  = false;
        private bool _isDark;
        private string _difficulty = "Mediu";

        // ── Timing ──
        private DispatcherTimer _timer    = new();
        private DateTime        _levelStart;
        private double          _timerBarMaxWidth = 0;

        // ── Symbols ──
        // Each level uses shapes/symbols; the "odd one out" differs by one attribute
        private static readonly string[] _shapeGroups = {
            "●", "■", "▲", "◆", "★", "♥", "♠", "♣", "⬟", "⬡"
        };

        // Color palettes per level group
        private static readonly string[] _colors = {
            "#3B82F6", "#EF4444", "#22C55E", "#F59E0B",
            "#8B5CF6", "#EC4899", "#14B8A6", "#F97316"
        };

        // ── Per-round data ──
        private string _distractorSymbol = "";
        private string _targetSymbol     = "";
        private string _distractorColor  = "";
        private string _targetColor      = "";

        public VisualSearchGame(bool isDark)
        {
            InitializeComponent();
            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick    += Timer_Tick;

            this.Loaded += (_, _) => {
                _timerBarMaxWidth = TimerBar.ActualWidth == 0
                    ? ((Grid)TimerBar.Parent).ActualWidth
                    : TimerBar.ActualWidth;
            };
        }

        // ═══════════════════════════════════════════════════
        //  START / RESTART
        // ═══════════════════════════════════════════════════
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _difficulty = RbEasy.IsChecked == true ? "Ușor"
                        : RbHard.IsChecked == true ? "Dificil"
                        : "Mediu";
            TxtDifficulty.Text = _difficulty;

            _level  = 1;
            _score  = 0;
            _lives  = 3;
            _isPlaying = true;

            PanelInstruction.Visibility = Visibility.Collapsed;
            PanelGameOver.Visibility    = Visibility.Collapsed;
            PanelGame.Visibility        = Visibility.Visible;

            StartLevel();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            PanelGameOver.Visibility = Visibility.Collapsed;
            _level  = 1;
            _score  = 0;
            _lives  = 3;
            _isPlaying = true;
            PanelGame.Visibility = Visibility.Visible;
            StartLevel();
        }

        // ═══════════════════════════════════════════════════
        //  LEVEL SETUP
        // ═══════════════════════════════════════════════════
        private void StartLevel()
        {
            _timer.Stop();

            // Grid size: starts 4x4, grows every 2 levels, capped at 9x9
            int baseSize = _difficulty switch { "Ușor" => 3, "Dificil" => 5, _ => 4 };
            int gridSize = Math.Min(baseSize + (_level - 1) / 2, 9);
            int total    = gridSize * gridSize;

            // Time: starts 30s, shrinks by 2s per level, min 8s
            int baseTime = _difficulty switch { "Ușor" => 40, "Dificil" => 22, _ => 30 };
            _timeLeft = Math.Max(baseTime - (_level - 1) * 2, 8);

            // Pick symbols & colors
            var rng = new Random();
            int symbolIdx  = rng.Next(_shapeGroups.Length);
            int altIdx     = (symbolIdx + 1 + rng.Next(_shapeGroups.Length - 1)) % _shapeGroups.Length;
            int colorIdx   = rng.Next(_colors.Length);
            int altColorIdx = (colorIdx + 1 + rng.Next(_colors.Length - 1)) % _colors.Length;

            // What makes the odd-one-out different?
            // Level 1-3: different shape, same color
            // Level 4-6: same shape, different color
            // Level 7+:  different shape AND different color
            bool diffShape = _level <= 6 || _level % 2 == 1;
            bool diffColor = _level >= 4;

            _distractorSymbol = _shapeGroups[symbolIdx];
            _targetSymbol     = diffShape ? _shapeGroups[altIdx] : _shapeGroups[symbolIdx];
            _distractorColor  = _colors[colorIdx];
            _targetColor      = diffColor ? _colors[altColorIdx] : _colors[colorIdx];

            // Place target randomly
            _targetIndex = rng.Next(total);

            // UI
            TxtLevel.Text     = _level.ToString();
            TxtScore.Text     = _score.ToString();
            TxtLives.Text     = string.Concat(Enumerable.Repeat("❤️", _lives))
                                + string.Concat(Enumerable.Repeat("🖤", 3 - _lives));
            TxtTimer.Text     = _timeLeft.ToString();
            TxtRoundInfo.Text = $"Nivel {_level}  •  Grilă {gridSize}×{gridSize}  •  {total} elemente";

            string hint = diffShape && diffColor ? "formă și culoare diferite"
                        : diffShape              ? "formă diferită"
                                                 : "culoare diferită";
            TxtTarget.Text          = _targetSymbol;
            TxtInstructionDetail.Text = $"— Găsește elementul cu {hint}!";

            // Build grid
            GameGrid.Columns = gridSize;
            GameGrid.Children.Clear();

            int cellSize = Math.Max(52 - gridSize * 2, 34);
            int fontSize = Math.Max(cellSize - 14, 14);

            for (int i = 0; i < total; i++)
            {
                bool isTarget = i == _targetIndex;
                string sym   = isTarget ? _targetSymbol    : _distractorSymbol;
                string col   = isTarget ? _targetColor     : _distractorColor;

                var btn = new Button
                {
                    Width  = cellSize,
                    Height = cellSize,
                    Margin = new Thickness(3),
                    Tag    = isTarget,
                    Background    = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(
                            _isDark ? "#1E293B" : "#F1F5F9")),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // Custom template
                var tpl  = new ControlTemplate(typeof(Button));
                var fef  = new FrameworkElementFactory(typeof(Border));
                fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                fef.SetBinding(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                    });
                var tb = new FrameworkElementFactory(typeof(TextBlock));
                tb.SetValue(TextBlock.TextProperty,   sym);
                tb.SetValue(TextBlock.FontSizeProperty, (double)fontSize);
                tb.SetValue(TextBlock.ForegroundProperty,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(col)));
                tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                tb.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
                fef.AppendChild(tb);
                tpl.VisualTree = fef;
                btn.Template   = tpl;

                int captured = i;
                btn.Click += Cell_Click;
                GameGrid.Children.Add(btn);
            }

            // Timer bar max width (after render)
            Dispatcher.InvokeAsync(() => {
                var container = (Grid)TimerBar.Parent;
                _timerBarMaxWidth = container.ActualWidth;
                UpdateTimerBar();
            }, DispatcherPriority.Loaded);

            _levelStart = DateTime.Now;
            _timer.Start();
        }

        // ═══════════════════════════════════════════════════
        //  CELL CLICK
        // ═══════════════════════════════════════════════════
        private void Cell_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlaying) return;

            var btn     = (Button)sender;
            bool isTarget = (bool)btn.Tag;

            if (isTarget)
            {
                _timer.Stop();
                double elapsed = (DateTime.Now - _levelStart).TotalSeconds;
                int bonus = Math.Max(0, (int)(_timeLeft * 10));
                int levelScore = 100 + bonus + (_level * 20);
                _score += levelScore;

                ShowFeedback("✅", $"+{levelScore} puncte!", $"Nivel {_level} completat în {elapsed:F1}s");

                _level++;
                Dispatcher.InvokeAsync(() => StartLevel(), DispatcherPriority.Background,
                    System.Threading.CancellationToken.None)
                    .Task.ContinueWith(_ => { },
                        System.Threading.Tasks.TaskScheduler.Default);

                // Delay next level
                var nextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                nextTimer.Tick += (_, _) => {
                    nextTimer.Stop();
                    PanelFeedback.Visibility = Visibility.Collapsed;
                    StartLevel();
                };
                nextTimer.Start();
            }
            else
            {
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
                    var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                    flashTimer.Tick += (_, _) => { flashTimer.Stop(); PanelFeedback.Visibility = Visibility.Collapsed; };
                    flashTimer.Start();
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  TIMER
        // ═══════════════════════════════════════════════════
        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timeLeft--;
            TxtTimer.Text = _timeLeft.ToString();
            UpdateTimerBar();

            // Color warning
            if (_timeLeft <= 5)
                TxtTimer.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));
            else if (_timeLeft <= 10)
                TxtTimer.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F59E0B"));
            else
                TxtTimer.Foreground = (SolidColorBrush)FindResource("TxtPrimary");

            if (_timeLeft <= 0)
            {
                _timer.Stop();
                _lives--;
                TxtLives.Text = string.Concat(Enumerable.Repeat("❤️", Math.Max(_lives, 0)))
                              + string.Concat(Enumerable.Repeat("🖤", 3 - Math.Max(_lives, 0)));

                if (_lives <= 0) EndGame(false);
                else
                {
                    ShowFeedback("⏰", "Timp expirat!", $"Mai ai {_lives} {(_lives == 1 ? "viață" : "vieți")}");
                    var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                    t.Tick += (_, _) => { t.Stop(); PanelFeedback.Visibility = Visibility.Collapsed; StartLevel(); };
                    t.Start();
                }
            }
        }

        private void UpdateTimerBar()
        {
            if (_timerBarMaxWidth <= 0) return;
            // Reconstruct max time from difficulty/level
            int baseTime = _difficulty switch { "Ușor" => 40, "Dificil" => 22, _ => 30 };
            int maxTime  = Math.Max(baseTime - (_level - 1) * 2, 8);
            double ratio = Math.Max(0, (double)_timeLeft / maxTime);
            TimerBar.Width = _timerBarMaxWidth * ratio;

            TimerBar.Background = ratio > 0.5
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"))
                : ratio > 0.25
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        }

        // ═══════════════════════════════════════════════════
        //  GAME OVER
        // ═══════════════════════════════════════════════════
        private void EndGame(bool won)
        {
            _isPlaying = false;
            _timer.Stop();
            PanelGame.Visibility     = Visibility.Collapsed;
            PanelFeedback.Visibility = Visibility.Collapsed;
            PanelGameOver.Visibility = Visibility.Visible;

            TxtGameOverIcon.Text  = won ? "🏆" : (_level > 5 ? "😤" : "💀");
            TxtGameOverTitle.Text = won ? "Felicitări!" : "Game Over";
            TxtGameOverSub.Text   = won
                ? $"Ai completat toate nivelurile cu scorul {_score}!"
                : $"Ai ajuns până la nivelul {_level}. Scorul tău este {_score}.";
            TxtFinalScore.Text    = _score.ToString();
            TxtFinalDetails.Text  = $"Nivel maxim: {_level}  •  Dificultate: {_difficulty}";

            _ = SaveActivityAsync();
        }

        // ═══════════════════════════════════════════════════
        //  FEEDBACK OVERLAY
        // ═══════════════════════════════════════════════════
        private void ShowFeedback(string icon, string msg, string sub)
        {
            TxtFeedbackIcon.Text = icon;
            TxtFeedbackMsg.Text  = msg;
            TxtFeedbackSub.Text  = sub;
            PanelFeedback.Visibility = Visibility.Visible;
        }

        // ═══════════════════════════════════════════════════
        //  SAVE TO FIREBASE
        // ═══════════════════════════════════════════════════
        private async System.Threading.Tasks.Task SaveActivityAsync()
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                double normalizedScore = Math.Min(100.0, _score / 10.0);

                var payload = new
                {
                    game       = "Visual Search",
                    difficulty = _difficulty,
                    scor       = Math.Round(normalizedScore, 2),
                    rawScore   = _score,
                    level      = _level,
                    dateTime   = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                    duration   = $"{(int)(DateTime.Now - _levelStart).TotalSeconds}s"
                };

                string json  = JsonSerializer.Serialize(payload);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                string url   = $"{_dbUrl}/{uid}/activities.json?auth={token}";
                await _http.PostAsync(url, content);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  NAVIGATION
        // ═══════════════════════════════════════════════════
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isPlaying = false;
            Owner?.Show();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            Owner?.Show();
        }

        // ═══════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════
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