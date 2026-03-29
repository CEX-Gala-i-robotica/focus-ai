using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class Dashboard : Window
    {
        // ── Serial ──
        private SerialPort? _serialPort;
        private Thread?     _readThread;
        private bool        _isRunning = true;

        // ── Firebase ──
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();

        // ── Registry ──
        private const string RegPath = @"Software\FocusAI";

        // ── Theme ──
        private bool _isDark = true;

        // ── Data ──
        private List<TestEntry> _testsCache = new();
        private record TestEntry(string Id, string DateTime, string Duration, double Scor, string MapRaw);

        // ═══════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════

        public Dashboard()
        {
            InitializeComponent();

            // Determină tema sistemului
            _isDark = IsSystemDarkTheme();

            // Aplică tema la nivelul întregii aplicații
            ThemeManager.Apply(_isDark);

            // Actualizează pictograma butonului de temă
            ThemeIcon.Text = _isDark ? "☀️" : "🌙";

            // Încarcă datele utilizatorului
            LoadUserInfoFromRegistry();
            _ = LoadProfileFromFirebaseAsync();

            // Inițializează portul serial
            InitializeSerialPort();

            // Înregistrează evenimentul de închidere
            this.Closing += Dashboard_Closing;

            // Încarcă testările
            _ = LoadTestsFromFirebaseAsync();
        }

        // ═══════════════════════════════════════════════════
        //  THEME
        // ═══════════════════════════════════════════════════

        private static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                return val is int i && i == 0;
            }
            catch { return true; }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            ThemeManager.Apply(_isDark);
            ThemeIcon.Text = _isDark ? "☀️" : "🌙";

            // Re‑randăm testele pentru a actualiza culorile rândurilor
            if (_testsCache.Count > 0)
                RenderTests(_testsCache);
        }

        // ═══════════════════════════════════════════════════
        //  TAB NAVIGATION
        // ═══════════════════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelProfil == null) return;
            PanelProfil.Visibility     = Visibility.Collapsed;
            PanelTestari.Visibility    = Visibility.Collapsed;
            PanelActivitati.Visibility = Visibility.Collapsed;

            if      (sender == TabProfil)  PanelProfil.Visibility     = Visibility.Visible;
            else if (sender == TabTestari) PanelTestari.Visibility    = Visibility.Visible;
            else                           PanelActivitati.Visibility = Visibility.Visible;
        }

        // ═══════════════════════════════════════════════════
        //  USER INFO — REGISTRY
        // ═══════════════════════════════════════════════════

        private void LoadUserInfoFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegPath);
                if (key == null) return;

                string email    = key.GetValue("Email")?.ToString()       ?? "";
                string name     = key.GetValue("Name")?.ToString()        ?? "";
                string surname  = key.GetValue("Surname")?.ToString()     ?? "";
                string phone    = key.GetValue("Phone")?.ToString()       ?? "";
                string docEmail = key.GetValue("DoctorEmail")?.ToString() ?? "";

                ApplyProfileToUI(email, name, surname, phone, docEmail);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  USER INFO — FIREBASE
        // ═══════════════════════════════════════════════════

        private async Task LoadProfileFromFirebaseAsync()
        {
            try
            {
                string uid   = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                string url  = $"{_dbUrl}/{uid}/profile.json?auth={token}";
                string json = await _http.GetStringAsync(url);
                if (string.IsNullOrEmpty(json) || json == "null") return;

                var profile = JsonSerializer.Deserialize<ProfileData>(json);
                if (profile == null) return;

                SaveProfileToRegistry(profile, GetReg("Email"));
                Dispatcher.Invoke(() =>
                    ApplyProfileToUI(GetReg("Email"),
                        profile.Name        ?? "",
                        profile.Surname     ?? "",
                        profile.Phone       ?? "",
                        profile.DoctorEmail ?? ""));
            }
            catch { }
        }

        private void ApplyProfileToUI(string email, string name, string surname,
                                      string phone, string docEmail)
        {
            string displayName = $"{name} {surname}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = email.Contains('@') ? email.Split('@')[0] : "Utilizator";

            ProfileFullName.Text = displayName;
            ProfileEmail.Text    = email;
            SidebarEmail.Text    = email;
            ProfilePhone.Text    = phone.Length > 0 ? $"📞 {phone}" : "";

            string initials = BuildInitials(name, surname, email);
            ProfileInitials.Text = initials;
            SidebarInitials.Text = initials;

            DoctorBadge.Visibility = string.IsNullOrEmpty(docEmail)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string BuildInitials(string name, string surname, string email)
        {
            string f = name.Length    > 0 ? name[0].ToString().ToUpper()    : "";
            string s = surname.Length > 0 ? surname[0].ToString().ToUpper() : "";
            if ((f + s).Length > 0) return f + s;
            return email.Length > 0 ? email[0].ToString().ToUpper() : "U";
        }

        private void SaveProfileToRegistry(ProfileData p, string email)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RegPath);
                if (!string.IsNullOrEmpty(p.Name))        k.SetValue("Name",        p.Name);
                if (!string.IsNullOrEmpty(p.Surname))     k.SetValue("Surname",     p.Surname);
                if (!string.IsNullOrEmpty(p.BirthDate))   k.SetValue("BirthDate",   p.BirthDate);
                if (!string.IsNullOrEmpty(p.Phone))       k.SetValue("Phone",       p.Phone);
                if (!string.IsNullOrEmpty(p.DoctorEmail)) k.SetValue("DoctorEmail", p.DoctorEmail);
                if (!string.IsNullOrEmpty(p.DoctorPhone)) k.SetValue("DoctorPhone", p.DoctorPhone);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  EDIT PROFILE
        // ═══════════════════════════════════════════════════

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var popup = new ProfileEditWindow(_isDark) { Owner = this };
            if (popup.ShowDialog() == true && popup.Result is not null)
                _ = LoadProfileFromFirebaseAsync();
        }

        // ═══════════════════════════════════════════════════
        //  FIREBASE — TESTS
        // ═══════════════════════════════════════════════════

        private async Task LoadTestsFromFirebaseAsync()
        {
            string uid   = GetReg("Uid");
            string token = GetReg("IdToken");

            if (string.IsNullOrEmpty(uid))
            {
                Dispatcher.Invoke(ShowTestEmpty);
                return;
            }

            Dispatcher.Invoke(ShowTestLoading);

            try
            {
                string json = await _http.GetStringAsync(
                    $"{_dbUrl}/{uid}/tests.json?auth={token}");

                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    Dispatcher.Invoke(() => { ShowTestEmpty(); UpdateProfileStats(new()); });
                    return;
                }

                var tests = ParseTests(json);
                Dispatcher.Invoke(() => RenderTests(tests));
            }
            catch
            {
                Dispatcher.Invoke(ShowTestEmpty);
            }
        }

        private List<TestEntry> ParseTests(string json)
        {
            var list = new List<TestEntry>();
            using var doc = JsonDocument.Parse(json);

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var v     = entry.Value;
                string mapRaw = v.TryGetProperty("map",      out var mp)  ? mp.GetString()  ?? "" : "";
                string dt     = v.TryGetProperty("dateTime", out var dtv) ? dtv.GetString() ?? "" : "";
                string dur    = v.TryGetProperty("duration", out var dv)  ? dv.GetString()  ?? "" : "";
                double scor   = v.TryGetProperty("scor",     out var sv)  ? sv.GetDouble()       : 0;
                list.Add(new TestEntry(entry.Name, dt, dur, scor, mapRaw));
            }

            return list.OrderByDescending(t => t.DateTime).ToList();
        }

        private void RenderTests(List<TestEntry> tests)
        {
            _testsCache = tests;
            TestRowsPanel.Children.Clear();

            if (tests.Count == 0) { ShowTestEmpty(); UpdateProfileStats(tests); return; }

            TestLoadingState.Visibility = Visibility.Collapsed;
            TestEmptyState.Visibility   = Visibility.Collapsed;
            TestTableHeader.Visibility  = Visibility.Visible;

            BestScore.Text    = $"{tests.Max(t => t.Scor):F2}";
            AvgScore.Text     = $"{tests.Average(t => t.Scor):F2}";
            LastTestDate.Text = tests[0].DateTime;

            for (int i = 0; i < tests.Count; i++)
                TestRowsPanel.Children.Add(BuildTestRow(i + 1, tests[i]));

            UpdateProfileStats(tests);
        }

        private Border BuildTestRow(int idx, TestEntry t)
        {
            // Culorile sunt preluate din ThemeManager (prin resursele dinamice)
            var bgRow   = (SolidColorBrush)FindResource("RowBg");
            var bgNum   = (SolidColorBrush)FindResource("RowNumBg");
            var textPri = (SolidColorBrush)FindResource("TxtPrimary");
            var textSec = (SolidColorBrush)FindResource("TxtSecondary");
            var btnBg   = _isDark
                ? (SolidColorBrush)FindResource("BgNavActive")
                : (SolidColorBrush)FindResource("BgCardHover");
            var btnFg   = _isDark
                ? (SolidColorBrush)FindResource("AccentSecFg")
                : (SolidColorBrush)FindResource("AccentSecFg");

            var scoreColor = t.Scor >= 80
                ? (Color)ColorConverter.ConvertFromString("#22C55E")
                : t.Scor >= 50
                    ? (Color)ColorConverter.ConvertFromString("#FB923C")
                    : (Color)ColorConverter.ConvertFromString("#EF4444");

            var row = new Border
            {
                Background   = bgRow,
                CornerRadius = new CornerRadius(10),
                Padding      = new Thickness(18, 12, 18, 12),
                Margin       = new Thickness(0, 0, 0, 8)
            };

            if (!_isDark)
            {
                row.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = (Color)ColorConverter.ConvertFromString("#1A2236"),
                    BlurRadius  = 16,
                    ShadowDepth = 1,
                    Opacity     = 0.07,
                    Direction   = 270
                };
            }

            var g = new Grid();
            int[] widths = { 50, -1, 90, 110, 80, 80 };
            foreach (var w in widths)
                g.ColumnDefinitions.Add(w == -1
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w) });

            void Add(int col, UIElement el) { Grid.SetColumn(el, col); g.Children.Add(el); }

            var numBd = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(8),
                Background = bgNum, VerticalAlignment = VerticalAlignment.Center
            };
            numBd.Child = new TextBlock
            {
                Text = idx.ToString(), FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground          = textSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Add(0, numBd);

            Add(1, new TextBlock { Text = t.DateTime, FontSize = 13,
                Foreground = textPri, VerticalAlignment = VerticalAlignment.Center });

            Add(2, new TextBlock { Text = t.Duration, FontSize = 13,
                Foreground = textSec, VerticalAlignment = VerticalAlignment.Center });

            var scoreBd = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(
                    _isDark ? (byte)30 : (byte)20,
                    scoreColor.R, scoreColor.G, scoreColor.B)),
                CornerRadius        = new CornerRadius(8),
                Padding             = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center
            };
            scoreBd.Child = new TextBlock
            {
                Text = $"{t.Scor:F2}", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(scoreColor)
            };
            Add(3, scoreBd);

            var btn = new Button
            {
                Content             = "Detalii",
                FontSize            = 11, FontWeight = FontWeights.SemiBold,
                Background          = btnBg,
                Foreground          = btnFg,
                BorderThickness     = new Thickness(0),
                Cursor              = System.Windows.Input.Cursors.Hand,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding             = new Thickness(10, 4, 10, 4)
            };
            var tpl = new ControlTemplate(typeof(Button));
            var fef = new FrameworkElementFactory(typeof(Border));
            fef.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
            fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty,              new Thickness(10, 4, 10, 4));
            fef.AppendChild(cp);
            tpl.VisualTree = fef;
            btn.Template   = tpl;

            var captured = t;
            btn.Click += (_, _) => new TestDetailsWindow(captured.MapRaw).ShowDialog();
            Add(5, btn);

            row.Child = g;
            return row;
        }

        private void UpdateProfileStats(List<TestEntry> tests)
        {
            StatNrTestari.Text = tests.Count > 0 ? tests.Count.ToString() : "0";
            StatScorMediu.Text = tests.Count > 0 ? $"{tests.Average(t => t.Scor):F1}" : "—";
            if (StatNrActivitati.Text      == "—") StatNrActivitati.Text      = "0";
            if (StatScorMaxActivitati.Text == "—") StatScorMaxActivitati.Text = "—";
        }

        private void ShowTestLoading()
        {
            TestLoadingState.Visibility = Visibility.Visible;
            TestEmptyState.Visibility   = Visibility.Collapsed;
            TestTableHeader.Visibility  = Visibility.Collapsed;
            TestRowsPanel.Children.Clear();
        }

        private void ShowTestEmpty()
        {
            TestLoadingState.Visibility = Visibility.Collapsed;
            TestEmptyState.Visibility   = Visibility.Visible;
            TestTableHeader.Visibility  = Visibility.Collapsed;
        }

        private async void RefreshTests_Click(object sender, RoutedEventArgs e)
            => await LoadTestsFromFirebaseAsync();

        // ═══════════════════════════════════════════════════
        //  GAMES
        // ═══════════════════════════════════════════════════

        private void GameMemorie_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Jocul Memorie — în curând!", "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
        private void GameSecvente_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Secvențe — în curând!", "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
        private void GameMatematica_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Matematică Rapidă — în curând!", "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);

        // ═══════════════════════════════════════════════════
        //  NEW TEST
        // ═══════════════════════════════════════════════════

        private void NewTest_Click(object sender, RoutedEventArgs e)
        {
            var startTest = new StartTest(this, _isDark);
            startTest.Show();
            this.Hide();
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

        // ═══════════════════════════════════════════════════
        //  SERIAL PORT
        // ═══════════════════════════════════════════════════

        private void InitializeSerialPort()
        {
            try
            {
                _serialPort = new SerialPort("COM7", 115200) { NewLine = "\r\n", DtrEnable = true };
                _serialPort.Open();
                _readThread = new Thread(ReadSerial) { IsBackground = true };
                _readThread.Start();
            }
            catch { }
        }

        private void ReadSerial()
        {
            while (_isRunning)
            {
                try { _serialPort?.ReadExisting(); Thread.Sleep(100); }
                catch { }
            }
        }

        // ═══════════════════════════════════════════════════
        //  CLOSING / LOGOUT
        // ═══════════════════════════════════════════════════

        private void Dashboard_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isRunning = false;
            _readThread?.Join(500);
            if (_serialPort?.IsOpen == true) { _serialPort.Close(); _serialPort.Dispose(); }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("LoggedIn",   "0");
                key.SetValue("IdToken",    "");
                key.SetValue("RememberMe", "0");
                key.SetValue("Email",      "");
                key.SetValue("Uid",        "");
                key.SetValue("Name",       "");
                key.SetValue("Surname",    "");
            }
            catch { }

            new Login().Show();
            Close();
        }

        private void GameStroop_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Stroop Game — în curând!", "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GameVisualSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Visual Search — în curând!", "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ═══════════════════════════════════════════════════
    //  MODEL
    // ═══════════════════════════════════════════════════

    public class ProfileData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }

        [JsonPropertyName("birth-date")]
        public string? BirthDate { get; set; }

        [JsonPropertyName("phone-number")]
        public string? Phone { get; set; }

        [JsonPropertyName("doctor-email")]
        public string? DoctorEmail { get; set; }

        [JsonPropertyName("doctor-phone")]
        public string? DoctorPhone { get; set; }
    }
}