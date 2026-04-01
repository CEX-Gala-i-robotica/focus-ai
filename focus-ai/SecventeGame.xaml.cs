using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class SecventeGame : Window
    {
        // ── Firebase ──
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private const string RegPath = @"Software\FocusAI";

        // ── Theme ──
        private readonly bool _isDark;

        // ── Game state ──
        private enum GamePhase { Idle, Showing, Waiting, Feedback }
        private GamePhase _phase = GamePhase.Idle;

        private readonly List<int> _sequence    = new();
        private readonly List<int> _playerInput = new();
        private int  _currentInputIndex = 0;
        private int  _level             = 1;
        private int  _lives             = 3;
        private string _difficulty      = "Ușor";

        // ── Numărul maxim de runde (nivele) ──
        private const int MaxLevels = 20;

        // ── Timing (ms) ──
        private int _flashOnMs  = 600;
        private int _flashOffMs = 250;
        private int _delayMs    = 400;

        // ── Buttons ──
        private Button[] _buttons = Array.Empty<Button>();

        // ── Button colors per index ──
        private static readonly string[] _colorsBase =
        {
            "#EF4444", "#3B82F6", "#22C55E", "#F59E0B",
            "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16", "#F97316",
        };

        // ── Session timing ──
        private DateTime _sessionStart;

        // ═══════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════
        public SecventeGame(bool isDark)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _buttons = new[] { Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8 };

            StyleButtons();
            this.Closing += SecventeGame_Closing;
        }

        // ═══════════════════════════════════════════════════
        //  BUTTON STYLING
        // ═══════════════════════════════════════════════════
        private void StyleButtons()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                var btn  = _buttons[i];
                var col  = (Color)ColorConverter.ConvertFromString(_colorsBase[i]);
                var dimBg = Color.FromArgb(_isDark ? (byte)30 : (byte)18, col.R, col.G, col.B);

                var tpl = new ControlTemplate(typeof(Button));
                var fef = new FrameworkElementFactory(typeof(Border));
                fef.SetValue(Border.BackgroundProperty, new SolidColorBrush(dimBg));
                fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
                fef.AppendChild(cp);
                tpl.VisualTree = fef;

                btn.Template        = tpl;
                btn.BorderThickness = new Thickness(0);
                btn.Cursor          = System.Windows.Input.Cursors.Hand;
                btn.IsEnabled       = false;

                btn.Effect = new DropShadowEffect
                {
                    Color       = col,
                    BlurRadius  = 0,
                    ShadowDepth = 0,
                    Opacity     = 0
                };
            }
        }

        // ═══════════════════════════════════════════════════
        //  FLASH helpers
        // ═══════════════════════════════════════════════════
        private async Task FlashButton(int idx, bool isCorrect = true)
        {
            var btn = _buttons[idx];
            var col = (Color)ColorConverter.ConvertFromString(_colorsBase[idx]);

            SetButtonColor(btn, col, lit: true);
            await Task.Delay(_flashOnMs);
            SetButtonColor(btn, col, lit: false);
            await Task.Delay(_flashOffMs);
        }

        private async Task FlashButtonError(int idx)
        {
            var btn      = _buttons[idx];
            var errorCol = (Color)ColorConverter.ConvertFromString("#EF4444");

            SetButtonColor(btn, errorCol, lit: true);
            await Task.Delay(400);
            var orig = (Color)ColorConverter.ConvertFromString(_colorsBase[idx]);
            SetButtonColor(btn, orig, lit: false);
        }

        private void SetButtonColor(Button btn, Color col, bool lit)
        {
            var border = GetBorder(btn);
            if (border == null) return;

            var alpha = lit ? (byte)220 : (_isDark ? (byte)30 : (byte)18);
            border.Background = new SolidColorBrush(Color.FromArgb(alpha, col.R, col.G, col.B));

            btn.Effect = new DropShadowEffect
            {
                Color       = col,
                BlurRadius  = lit ? 24 : 0,
                ShadowDepth = 0,
                Opacity     = lit ? 0.9 : 0
            };
        }

        private static Border? GetBorder(Button btn)
        {
            btn.ApplyTemplate();
            return VisualTreeHelper.GetChildrenCount(btn) > 0
                ? VisualTreeHelper.GetChild(btn, 0) as Border
                : null;
        }

        // ═══════════════════════════════════════════════════
        //  SCORUL CURENT (0..100)
        // ═══════════════════════════════════════════════════
        private double GetCurrentScore()
        {
            int completed = Math.Max(0, _level - 1);
            if (completed == 0 && _lives == 0) return 0;
            double raw = (completed * (3 + _lives)) / (double)(MaxLevels * 6) * 100;
            return Math.Min(100, Math.Max(0, raw));
        }

        // ═══════════════════════════════════════════════════
        //  START GAME
        // ═══════════════════════════════════════════════════
        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            _difficulty = DiffEasy.IsChecked == true   ? "Ușor"
                        : DiffMedium.IsChecked == true ? "Mediu"
                        : "Dificil";

            ApplyDifficultySettings();

            _level = 1;
            _lives = 3;
            _sequence.Clear();
            _sessionStart = DateTime.Now;

            WinOverlay.Visibility = Visibility.Collapsed;

            ScreenMenu.Visibility = Visibility.Collapsed;
            ScreenGame.Visibility = Visibility.Visible;

            UpdateHUD();
            _ = NextRound();
        }

        private void ApplyDifficultySettings()
        {
            switch (_difficulty)
            {
                case "Ușor":
                    _flashOnMs  = 700;
                    _flashOffMs = 300;
                    _delayMs    = 500;
                    break;
                case "Mediu":
                    _flashOnMs  = 500;
                    _flashOffMs = 200;
                    _delayMs    = 350;
                    break;
                case "Dificil":
                    _flashOnMs  = 350;
                    _flashOffMs = 150;
                    _delayMs    = 250;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════
        //  ROUND LOGIC
        // ═══════════════════════════════════════════════════
        private async Task NextRound()
        {
            if (_level > MaxLevels)
            {
                ShowGameOver(won: true);
                return;
            }

            _phase = GamePhase.Showing;
            SetButtonsEnabled(false);

            var rng = new Random();
            _sequence.Add(rng.Next(0, 9));
            _playerInput.Clear();
            _currentInputIndex = 0;

            UpdateHUD();
            UpdateProgress(0, _sequence.Count);
            StatusMessage.Text = $"🔵  Urmărește secvența... (Nivel {_level}/{MaxLevels})";

            await Task.Delay(_delayMs + 200);

            for (int i = 0; i < _sequence.Count; i++)
            {
                await FlashButton(_sequence[i]);
                await Task.Delay(_flashOffMs);
            }

            _phase = GamePhase.Waiting;
            StatusMessage.Text = "👆  Reproduce secvența!";
            SetButtonsEnabled(true);
        }

        // ═══════════════════════════════════════════════════
        //  PLAYER INPUT
        // ═══════════════════════════════════════════════════
        private async void SeqButton_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != GamePhase.Waiting) return;

            var btn     = (Button)sender;
            int pressed  = int.Parse(btn.Tag.ToString()!);
            int expected = _sequence[_currentInputIndex];

            _ = FlashButton(pressed);

            if (pressed == expected)
            {
                _currentInputIndex++;
                int total = _sequence.Count;
                UpdateProgress(_currentInputIndex, total);

                if (_currentInputIndex == total)
                {
                    _phase = GamePhase.Feedback;
                    SetButtonsEnabled(false);

                    _level++;
                    StatusMessage.Text = $"✅  Corect! Scor curent: {GetCurrentScore():F0}/100";
                    UpdateHUD();

                    await Task.Delay(900);
                    _ = NextRound();
                }
            }
            else
            {
                _phase = GamePhase.Feedback;
                SetButtonsEnabled(false);

                await FlashButtonError(pressed);

                _lives--;
                UpdateHUD();

                if (_lives <= 0)
                {
                    await Task.Delay(300);
                    ShowGameOver(won: false);
                }
                else
                {
                    StatusMessage.Text = $"❌  Greșit! Mai ai {_lives} {"viață".PluralRo(_lives)}. Reluăm secvența...";
                    await Task.Delay(1200);

                    _playerInput.Clear();
                    _currentInputIndex = 0;
                    UpdateProgress(0, _sequence.Count);
                    _phase = GamePhase.Showing;

                    await Task.Delay(400);
                    for (int i = 0; i < _sequence.Count; i++)
                    {
                        await FlashButton(_sequence[i]);
                        await Task.Delay(_flashOffMs);
                    }

                    _phase = GamePhase.Waiting;
                    StatusMessage.Text = "👆  Reproduce secvența!";
                    SetButtonsEnabled(true);
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  HUD + PROGRESS
        // ═══════════════════════════════════════════════════
        private void UpdateHUD()
        {
            LevelText.Text = $"{Math.Min(_level, MaxLevels)}/{MaxLevels}";
            ScoreText.Text = $"{GetCurrentScore():F0} / 100";
            LivesText.Text = _lives switch
            {
                3 => "❤️ ❤️ ❤️",
                2 => "❤️ ❤️ 🖤",
                1 => "❤️ 🖤 🖤",
                _ => "🖤 🖤 🖤"
            };
        }

        private void UpdateProgress(int done, int total)
        {
            if (total == 0) { ProgressBar.Width = 0; return; }
            double ratio = (double)done / total;
            ProgressBar.Width = 400 * ratio;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (var b in _buttons)
                b.IsEnabled = enabled;
        }

        // ═══════════════════════════════════════════════════
        //  GAME OVER / WIN
        // ═══════════════════════════════════════════════════
        private void ShowGameOver(bool won)
        {
            _phase = GamePhase.Idle;
            SetButtonsEnabled(false);

            int completedLevels = Math.Max(0, _level - 1);
            double finalScore = GetCurrentScore();

            string emoji, title;
            if (won || completedLevels == MaxLevels)
            {
                emoji = "🏆";
                title = "Felicitări! Ai terminat toate nivelele!";
            }
            else
            {
                emoji = _lives == 0 ? "💀" : "🎯";
                title = _lives == 0 ? "Game Over!" : "Bine jucat!";
            }

            WinEmoji.Text    = emoji;
            WinTitle.Text    = title;
            WinSubtitle.Text = $"Nivel atins: {completedLevels}/{MaxLevels}  •  Scor: {finalScore:F1}/100  •  Dificultate: {_difficulty}";
            WinScore.Text    = $"Scor final: {finalScore:F1} / 100";

            WinOverlay.Visibility = Visibility.Visible;

            _ = SaveSessionAsync(finalScore);
        }

        // ═══════════════════════════════════════════════════
        //  SAVE TO FIREBASE (MODIFIED: dateTime format & duration format)
        // ═══════════════════════════════════════════════════
        private async Task SaveSessionAsync(double finalScore)
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                var duration = DateTime.Now - _sessionStart;
                // Format duration as MM:SS with leading zeros (e.g., "01:11")
                string durationFormatted = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
                // Format dateTime as YYYY-MM-DD HH:MM:SS
                string dateTimeFormatted = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var payload = new
                {
                    dateTime   = dateTimeFormatted,
                    duration   = durationFormatted,
                    game       = "Secvențe",
                    difficulty = _difficulty,
                    scor       = Math.Round(finalScore, 2),
                    rawScore   = (int)Math.Round(finalScore),
                    maxLevel   = Math.Max(0, _level - 1)
                };

                string json  = JsonSerializer.Serialize(payload);
                string url   = $"{_dbUrl}/{uid}/activities.json?auth={token}";
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync(url, content);
            }
            catch { /* silently ignore */ }
        }

        // ═══════════════════════════════════════════════════
        //  NAV BUTTONS
        // ═══════════════════════════════════════════════════

        private void WinPlayAgain_Click(object sender, RoutedEventArgs e)
        {
            WinOverlay.Visibility = Visibility.Collapsed;
            ScreenGame.Visibility = Visibility.Collapsed;
            ScreenMenu.Visibility = Visibility.Visible;
        }

        private void WinGoMenu_Click(object sender, RoutedEventArgs e)
        {
            Owner?.Show();
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Owner?.Show();
            Close();
        }

        private void SecventeGame_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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

    // ── Romanian plural helper ──
    internal static class RoExtensions
    {
        internal static string PluralRo(this string singular, int count)
            => count == 1 ? singular : singular switch
            {
                "viață" => "vieți",
                _       => singular
            };
    }
}