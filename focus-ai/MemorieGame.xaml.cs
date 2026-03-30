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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class MemorieGame : Window
    {
        private const string RegPath = @"Software\FocusAI";
        private static readonly HttpClient _http = new();
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";

        private static readonly string[] CardSymbols =
        {
            "★","♠","♦","♣","♥","▲","●","■",
            "◆","♛","♜","♞","♟","⬟","⬡","✿"
        };

        private List<CardModel> _cards = new();
        private CardButton? _firstCard = null;
        private CardButton? _secondCard = null;
        private bool _isLocked = false;
        private int _moves = 0;
        private int _pairsFound = 0;
        private int _totalPairs = 8;
        private int _rows = 4;
        private int _cols = 4;
        private string _difficulty = "Ușor";

        private DispatcherTimer _timer = new();
        private int _seconds = 0;
        private bool _gameStarted = false;
        private readonly bool _isDark;
        private DateTime _gameStartTime;

        public class CardModel
        {
            public int Id { get; set; }
            public string Symbol { get; set; } = "";
            public bool IsFlipped { get; set; }
            public bool IsMatched { get; set; }
        }

        public MemorieGame(bool isDark)
        {
            InitializeComponent();
            _isDark = isDark;
            ThemeManager.Apply(_isDark);

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            this.Closing += (_, _) => Owner?.Show();
            StartNewGame();
        }

        private void Difficulty_Checked(object sender, RoutedEventArgs e)
        {
            if (CardGrid == null) return;

            if (sender == DiffEasy) { _rows = 4; _cols = 4; _difficulty = "Ușor"; }
            else if (sender == DiffMedium) { _rows = 4; _cols = 5; _difficulty = "Mediu"; }
            else { _rows = 4; _cols = 6; _difficulty = "Dificil"; }

            CardGrid.Rows = _rows;
            CardGrid.Columns = _cols;

            double baseW = _cols * 120 + (_cols - 1) * 8;
            double baseH = _rows * 110 + (_rows - 1) * 8;
            CardGrid.Width = baseW;
            CardGrid.Height = baseH;

            _totalPairs = (_rows * _cols) / 2;
            StartNewGame();
        }

        private void NewGame_Click(object sender, RoutedEventArgs e) => StartNewGame();

        private void StartNewGame()
        {
            _firstCard = null;
            _secondCard = null;
            _isLocked = false;
            _moves = 0;
            _pairsFound = 0;
            _seconds = 0;
            _gameStarted = false;
            _gameStartTime = DateTime.Now;

            WinOverlay.Visibility = Visibility.Collapsed;
            _timer.Stop();
            UpdateHUD();
            TimerText.Text = "00:00";

            string[] pool = CardSymbols.Take(_totalPairs).ToArray();
            var symbolList = pool.Concat(pool).ToList();
            var rng = new Random();
            for (int i = symbolList.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (symbolList[i], symbolList[j]) = (symbolList[j], symbolList[i]);
            }

            _cards = symbolList.Select((s, i) => new CardModel { Id = i, Symbol = s }).ToList();

            CardGrid.Children.Clear();
            foreach (var card in _cards)
            {
                var btn = new CardButton(card, _isDark);
                btn.Click += Card_Click;
                CardGrid.Children.Add(btn);
            }
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            if (_isLocked) return;
            if (sender is not CardButton clicked) return;
            if (clicked.Model.IsFlipped || clicked.Model.IsMatched) return;

            if (!_gameStarted) { _gameStarted = true; _timer.Start(); }

            clicked.FlipToFront();

            if (_firstCard == null)
            {
                _firstCard = clicked;
                return;
            }

            _secondCard = clicked;
            _moves++;
            UpdateHUD();

            if (_firstCard.Model.Symbol == _secondCard.Model.Symbol)
            {
                _firstCard.Model.IsMatched = true;
                _secondCard.Model.IsMatched = true;
                _firstCard.SetMatched();
                _secondCard.SetMatched();
                _pairsFound++;
                UpdateHUD();
                _firstCard = _secondCard = null;

                if (_pairsFound == _totalPairs)
                    Dispatcher.InvokeAsync(ShowWin, DispatcherPriority.Background);
            }
            else
            {
                _isLocked = true;
                var fc = _firstCard;
                var sc = _secondCard;
                _firstCard = _secondCard = null;

                var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                delay.Tick += (_, _) =>
                {
                    delay.Stop();
                    fc.FlipToBack();
                    sc.FlipToBack();
                    _isLocked = false;
                };
                delay.Start();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _seconds++;
            int m = _seconds / 60, s = _seconds % 60;
            TimerText.Text = $"{m:D2}:{s:D2}";
        }

        private void UpdateHUD()
        {
            MovesText.Text = _moves.ToString();
            PairsText.Text = $"{_pairsFound} / {_totalPairs}";
        }

        private void ShowWin()
        {
            _timer.Stop();
            double maxMoves = _totalPairs * 2.5;
            double movePen = Math.Max(0, (_moves - _totalPairs) / maxMoves * 40);
            double timePen = Math.Min(40, _seconds / 3.0);
            double score = Math.Max(0, 100 - movePen - timePen);

            int m = _seconds / 60, s = _seconds % 60;
            WinSubtitle.Text = $"Timp: {m:D2}:{s:D2}  •  Mișcări: {_moves}";
            WinScore.Text = $"Scor: {score:F1} / 100";
            WinOverlay.Visibility = Visibility.Visible;

            _ = SaveActivityToFirebaseAsync(score);
        }

        private async System.Threading.Tasks.Task SaveActivityToFirebaseAsync(double score)
        {
            try
            {
                string uid = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token)) return;

                var duration = TimeSpan.FromSeconds(_seconds);
                string durationStr = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";

                var payload = new
                {
                    game = "Memorie",
                    dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration = durationStr,
                    moves = _moves,
                    pairs = _totalPairs,
                    difficulty = _difficulty,
                    scor = Math.Round(score, 2)
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string url = $"{_dbUrl}/{uid}/activities.json?auth={token}";

                await _http.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] Eroare: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            Owner?.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
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

    public class CardButton : Button
    {
        public MemorieGame.CardModel Model { get; }

        private readonly Border _front;
        private readonly Border _back;
        private readonly Grid _inner;
        private readonly bool _isDark;

        private static readonly string[] AccentColors =
        {
            "#3B82F6","#8B5CF6","#EC4899","#F59E0B",
            "#10B981","#06B6D4","#EF4444","#84CC16",
            "#F97316","#6366F1","#14B8A6","#D946EF",
            "#0EA5E9","#A855F7","#22C55E","#FB923C"
        };

        private static int SlotOf(string symbol)
        {
            int h = 0;
            foreach (char c in symbol) h = (h * 31 + c) & 0x7FFFFFFF;
            return h % AccentColors.Length;
        }

        public CardButton(MemorieGame.CardModel model, bool isDark)
        {
            Model = model;
            _isDark = isDark;

            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            Cursor = System.Windows.Input.Cursors.Hand;
            Margin = new Thickness(4);

            string backBg = isDark ? "#1E293B" : "#F1F5F9";
            string backBorder = isDark ? "#334155" : "#CBD5E1";
            string backFg = isDark ? "#FFFFFF" : "#1E293B";

            _back = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backBg)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backBorder)),
                BorderThickness = new Thickness(2)
            };
            _back.Child = new TextBlock
            {
                Text = "?",
                FontSize = 34,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backFg)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            int slot = SlotOf(model.Symbol);
            string colorHex = AccentColors[slot];
            var fgColor = (Color)ColorConverter.ConvertFromString(colorHex);
            byte bgAlpha = isDark ? (byte)22 : (byte)45;
            var bgColor = Color.FromArgb(bgAlpha, fgColor.R, fgColor.G, fgColor.B);
            string symbolFg = isDark ? "#FFFFFF" : "#0F172A";

            _front = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(fgColor),
                BorderThickness = new Thickness(2),
                Visibility = Visibility.Collapsed
            };
            _front.Child = new TextBlock
            {
                Text = model.Symbol,
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(symbolFg)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _inner = new Grid();
            _inner.Children.Add(_back);
            _inner.Children.Add(_front);

            var tpl = new ControlTemplate(typeof(Button));
            var fef = new FrameworkElementFactory(typeof(Border));
            fef.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            fef.AppendChild(cp);
            tpl.VisualTree = fef;
            Template = tpl;
            Content = _inner;
        }

        public void FlipToFront()
        {
            if (Model.IsFlipped) return;
            Model.IsFlipped = true;
            AnimateFlip(_back, _front);
        }

        public void FlipToBack()
        {
            if (!Model.IsFlipped) return;
            Model.IsFlipped = false;
            AnimateFlip(_front, _back);
        }

        public void SetMatched()
        {
            int slot = SlotOf(Model.Symbol);
            string colorHex = AccentColors[slot];
            var fgColor = (Color)ColorConverter.ConvertFromString(colorHex);

            _front.BorderBrush = new SolidColorBrush(fgColor);
            _front.Background = new SolidColorBrush(Color.FromArgb(75, fgColor.R, fgColor.G, fgColor.B));

            var st = new ScaleTransform(1, 1);
            RenderTransform = st;
            RenderTransformOrigin = new Point(0.5, 0.5);

            var anim = new DoubleAnimation(1.0, 1.08, TimeSpan.FromMilliseconds(120))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

            IsEnabled = false;
            Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private static void AnimateFlip(UIElement hide, UIElement show)
        {
            var scaleHide = new ScaleTransform(1, 1);
            if (hide is FrameworkElement feHide)
            {
                feHide.RenderTransform = scaleHide;
                feHide.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var phase1 = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            phase1.Completed += (_, _) =>
            {
                hide.Visibility = Visibility.Collapsed;
                show.Visibility = Visibility.Visible;

                var scaleShow = new ScaleTransform(0, 1);
                if (show is FrameworkElement feShow)
                {
                    feShow.RenderTransform = scaleShow;
                    feShow.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                var phase2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                scaleShow.BeginAnimation(ScaleTransform.ScaleXProperty, phase2);
            };

            scaleHide.BeginAnimation(ScaleTransform.ScaleXProperty, phase1);
        }
    }
}