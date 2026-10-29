using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using focus_ai.Prediction;
using focus_ai.Prediction.Models;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class Dashboard : Window
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly string _dbUrl = AppSettings.Get("RealtimeDatabaseUrl");
        private static readonly HttpClient _http = new();

        private const string RegPath = @"Software\FocusAI";

        private bool _isDark = true;
        private bool _isLoadingTests = false;
        private bool _isLoadingActivities = false;
        private bool _isLoadingPatients = false;

        private List<TestEntry> _testsCache = new();
        private List<ActivityEntry> _activitiesCache = new();
        private List<PatientEntry> _patientsCache = new();
        private string _selectedPatientId = "";
        private string _selectedPatientName = "";

        private record TestEntry(string Id, string DateTime, DateTime RecordedAt, string Duration, double Scor, string MapRaw,
                                 SessionFeatures? Features);
        private record ActivityEntry(string Id, string DateTime, DateTime RecordedAt, string Duration,
                                     string Game, string Difficulty, double Scor);
        private record PatientEntry(string Id, string Name, string Email, string Phone,
                                    int TestCount, double TotalTestScore,
                                    int ActivityCount, double TotalActivityScore,
                                    PredictionSummary Prediction);
        private record PredictionSummary(string Direction, string Reason);

        public Dashboard()
        {
            InitializeComponent();
            WindowHelper.MoveToPrimaryMonitor(this);
            _isDark = true;
            ThemeManager.Apply(_isDark);
            ThemeIcon.Text = _isDark ? "☀️" : "🌙";
            LanguageManager.Register(this, LanguageToggleBtn);
            LanguageManager.LanguageChanged += Dashboard_LanguageChanged;

            LoadUserInfoFromRegistry();
            _ = LoadProfileFromFirebaseAsync();

            this.Closing += Dashboard_Closing;

            _ = LoadPatientsFromFirebaseAsync();
        }

        private void Dashboard_LanguageChanged(object? sender, EventArgs e)
        {
            if (_patientsCache.Count > 0) RenderPatients(_patientsCache);
            if (_testsCache.Count > 0) RenderTests(_testsCache);
            if (_activitiesCache.Count > 0) RenderActivities(_activitiesCache);
        }

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

        private void NewTest_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePatientHistoryUnlocked(LanguageManager.T("Select a patient to view test history"))) return;

            var startTest = new StartTest(this, _isDark);
            startTest.Owner = this;
            startTest.Closed += async (s, args) =>
            {
                await LoadTestsFromFirebaseAsync();
                if (PanelActivitati.Visibility == Visibility.Visible)
                {
                    await LoadActivitiesFromFirebaseAsync();
                }
            };
            startTest.Show();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            ThemeManager.Apply(_isDark);
            ThemeIcon.Text = _isDark ? "☀️" : "🌙";

            if (_testsCache.Count > 0)
                RenderTests(_testsCache);
            if (_activitiesCache.Count > 0)
                RenderActivities(_activitiesCache);
            if (_patientsCache.Count > 0)
                RenderPatients(_patientsCache);
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelProfil == null) return;
            PanelProfil.Visibility = Visibility.Collapsed;
            PanelPacienti.Visibility = Visibility.Collapsed;
            PanelTestari.Visibility = Visibility.Collapsed;
            PanelActivitati.Visibility = Visibility.Collapsed;

            if (sender == TabProfil)
            {
                PanelProfil.Visibility = Visibility.Visible;
            }
            else if (sender == TabPacienti)
            {
                PanelPacienti.Visibility = Visibility.Visible;
                if (_patientsCache.Count == 0 || PatientsLoadingState.Visibility == Visibility.Visible)
                {
                    _ = LoadPatientsFromFirebaseAsync();
                }
            }
            else if (sender == TabTestari)
            {
                if (!EnsurePatientHistoryUnlocked(LanguageManager.T("Select a patient to view test history")))
                {
                    return;
                }

                PanelTestari.Visibility = Visibility.Visible;
                if (_testsCache.Count == 0 || TestLoadingState.Visibility == Visibility.Visible)
                {
                    _ = LoadTestsFromFirebaseAsync();
                }
            }
            else
            {
                if (!EnsurePatientHistoryUnlocked(LanguageManager.T("Select a patient to view activity history")))
                {
                    return;
                }

                PanelActivitati.Visibility = Visibility.Visible;
                if (_activitiesCache.Count == 0 && ActLoadingState.Visibility != Visibility.Visible)
                {
                    _ = LoadActivitiesFromFirebaseAsync();
                }
            }
        }

        private void LoadUserInfoFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegPath);
                if (key == null) return;

                string email = key.GetValue("Email")?.ToString() ?? "";
                string name = key.GetValue("Name")?.ToString() ?? "";
                string surname = key.GetValue("Surname")?.ToString() ?? "";
                string phone = key.GetValue("Phone")?.ToString() ?? "";
                string docEmail = key.GetValue("DoctorEmail")?.ToString() ?? "";

                ApplyProfileToUI(email, name, surname, phone, docEmail);
            }
            catch { }
        }

        private async Task LoadProfileFromFirebaseAsync()
        {
            try
            {
                string uid = GetReg("Uid");
                string token = GetReg("IdToken");
                if (string.IsNullOrEmpty(uid)) return;

                string url = $"{_dbUrl}/doctors/{uid}.json?auth={token}";
                string json = await _http.GetStringAsync(url, _cts.Token);
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync($"{_dbUrl}/{uid}/profile.json?auth={token}", _cts.Token);
                    if (string.IsNullOrEmpty(json) || json == "null") return;
                }

                var profile = JsonSerializer.Deserialize<ProfileData>(json);
                if (profile == null) return;

                SaveProfileToRegistry(profile, GetReg("Email"));
                Dispatcher.Invoke(() =>
                    ApplyProfileToUI(GetReg("Email"),
                        profile.Name ?? "",
                        profile.Surname ?? "",
                        profile.EffectivePhone,
                        profile.DoctorEmail ?? ""));
            }
            catch { }
        }

        private void ApplyProfileToUI(string email, string name, string surname,
                                      string phone, string docEmail)
        {
            string displayName = $"{name} {surname}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = email.Contains('@') ? email.Split('@')[0] : "User";

            ProfileFullName.Text = displayName;
            ProfileEmail.Text = email;
            SidebarEmail.Text = email;
            ProfilePhone.Text = phone.Length > 0 ? phone : "";

            string initials = BuildInitials(name, surname, email);
            ProfileInitials.Text = initials;
            SidebarInitials.Text = initials;

            DoctorBadge.Visibility = Visibility.Visible;
        }

        private static string BuildInitials(string name, string surname, string email)
        {
            string f = name.Length > 0 ? name[0].ToString().ToUpper() : "";
            string s = surname.Length > 0 ? surname[0].ToString().ToUpper() : "";
            if ((f + s).Length > 0) return f + s;
            return email.Length > 0 ? email[0].ToString().ToUpper() : "U";
        }

        private void SaveProfileToRegistry(ProfileData p, string email)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RegPath);
                if (!string.IsNullOrEmpty(p.Name)) k.SetValue("Name", p.Name);
                if (!string.IsNullOrEmpty(p.Surname)) k.SetValue("Surname", p.Surname);
                if (!string.IsNullOrEmpty(p.EffectiveBirthDate)) k.SetValue("BirthDate", p.EffectiveBirthDate);
                if (!string.IsNullOrEmpty(p.EffectivePhone)) k.SetValue("Phone", p.EffectivePhone);
                if (!string.IsNullOrEmpty(p.DoctorEmail)) k.SetValue("DoctorEmail", p.DoctorEmail);
                if (!string.IsNullOrEmpty(p.Adress)) k.SetValue("Adress", p.Adress);
            }
            catch { }
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var popup = new ProfileEditWindow(_isDark) { Owner = this };
            if (popup.ShowDialog() == true && popup.Result is not null)
                _ = LoadProfileFromFirebaseAsync();
        }

        private async Task LoadTestsFromFirebaseAsync()
        {
            if (_isLoadingTests) return;
            _isLoadingTests = true;

            try
            {
                string uid = GetActivePatientId();
                string token = GetReg("IdToken");

                if (!CanAccessSelectedPatientHistory())
                {
                    Dispatcher.Invoke(ShowTestEmpty);
                    return;
                }

                Dispatcher.Invoke(ShowTestLoading);

                string json = await _http.GetStringAsync(
                    $"{_dbUrl}/patients/{uid}/testResults.json?auth={token}", _cts.Token);
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/testResults/{uid}.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/tests.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/testResults.json?auth={token}", _cts.Token);
                }

                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    Dispatcher.Invoke(() => { ShowTestEmpty(); UpdateProfileStats(new(), _activitiesCache); });
                    return;
                }

                var tests = ParseTests(json);
                Dispatcher.Invoke(() => RenderTests(tests));
            }
            catch
            {
                Dispatcher.Invoke(ShowTestEmpty);
            }
            finally
            {
                _isLoadingTests = false;
            }
        }

        private List<TestEntry> ParseTests(string json)
        {
            var list = new List<TestEntry>();
            using var doc = JsonDocument.Parse(json);

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var v = entry.Value;
                string mapRaw = v.TryGetProperty("map", out var mp) ? mp.GetString() ?? "" : "";
                string dt = v.TryGetProperty("dateTime", out var dtv) ? dtv.GetString() ?? "" : "";
                string dur = v.TryGetProperty("duration", out var dv) ? dv.GetString() ?? "" : "";
                double storedScore = v.TryGetProperty("scor", out var sv) && sv.ValueKind == JsonValueKind.Number ? sv.GetDouble() : 0;
                SessionFeatures? features = TryExtractFeatures(v);
                double scor = features is { HasScoreData: true } ? features.Score : storedScore;
                DateTime recordedAt = features?.DateTime is { } featureDate && featureDate != DateTime.MinValue
                    ? featureDate
                    : ParseRecordedAt(dt);
                list.Add(new TestEntry(entry.Name, dt, recordedAt, dur, scor, mapRaw, features));
            }

            return list.OrderByDescending(t => t.RecordedAt).ToList();
        }

        private static SessionFeatures? TryExtractFeatures(JsonElement element)
        {
            try
            {
                return FeatureExtractor.FromJsonElement(element);
            }
            catch
            {
                return null;
            }
        }

        private static CultureInfo UiCulture => LanguageManager.Current == AppLanguage.Romanian
            ? CultureInfo.GetCultureInfo("ro-RO")
            : CultureInfo.GetCultureInfo("en-US");

        private static DateTime ParseRecordedAt(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;

            string[] formats =
            {
                "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "O"
            };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out DateTime exact))
                return exact;

            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }

        private static string FormatRecordedAt(DateTime value, string fallback)
            => value == DateTime.MinValue ? fallback : value.ToString("dd MMM yyyy, HH:mm", UiCulture);

        private static string FormatScore(double value) => value.ToString("F2", UiCulture);

        private (string Background, string Foreground) GetScorePalette(double score)
        {
            if (!double.IsFinite(score))
                return _isDark ? ("#1E293B", "#CBD5E1") : ("#F1F5F9", "#475569");

            return score switch
            {
                >= 80 => _isDark ? ("#153B2A", "#86EFAC") : ("#DCFCE7", "#15803D"),
                >= 50 => _isDark ? ("#4A3213", "#FCD34D") : ("#FEF3C7", "#B45309"),
                _ => _isDark ? ("#451A1A", "#FCA5A5") : ("#FEE2E2", "#B91C1C")
            };
        }

        private (string Background, string Foreground) GetDifficultyPalette(string difficulty)
        {
            string normalized = difficulty.Trim().ToLowerInvariant();

            return normalized switch
            {
                "easy" or "ușor" or "usor" => _isDark ? ("#153B2A", "#86EFAC") : ("#DCFCE7", "#15803D"),
                "medium" or "mediu" => _isDark ? ("#4A3213", "#FCD34D") : ("#FEF3C7", "#B45309"),
                "hard" or "dificil" or "greu" => _isDark ? ("#451A1A", "#FCA5A5") : ("#FEE2E2", "#B91C1C"),
                _ => _isDark ? ("#1E293B", "#CBD5E1") : ("#F1F5F9", "#475569")
            };
        }

        private Border BuildScoreBadge(double score)
        {
            var (background, foreground) = GetScorePalette(score);
            var foregroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));

            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background)),
                BorderBrush = foregroundBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = FormatScore(score),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = foregroundBrush
                }
            };
        }

        private void RenderTests(List<TestEntry> tests)
        {
            _testsCache = tests;
            TestRowsPanel.Children.Clear();

            if (tests.Count == 0)
            {
                ShowTestEmpty();
                UpdateProfileStats(tests, _activitiesCache);
                return;
            }

            TestLoadingState.Visibility = Visibility.Collapsed;
            TestEmptyState.Visibility = Visibility.Collapsed;
            TestTableHeader.Visibility = Visibility.Visible;

            BestScore.Text = FormatScore(tests.Max(t => t.Scor));
            AvgScore.Text = FormatScore(tests.Average(t => t.Scor));
            LastTestDate.Text = FormatRecordedAt(tests[0].RecordedAt, tests[0].DateTime);

            for (int i = 0; i < tests.Count; i++)
                TestRowsPanel.Children.Add(BuildTestRow(i + 1, tests[i]));

            UpdateProfileStats(tests, _activitiesCache);
            LanguageManager.Apply(this);
        }

        private Border BuildTestRow(int idx, TestEntry t)
        {
            var bgRow = (SolidColorBrush)FindResource("RowBg");
            var bgNum = (SolidColorBrush)FindResource("RowNumBg");
            var rowHover = (SolidColorBrush)FindResource("RowHover");
            var rowBorder = (SolidColorBrush)FindResource("RowBorder");
            var textPri = (SolidColorBrush)FindResource("TxtPrimary");
            var textSec = (SolidColorBrush)FindResource("TxtSecondary");
            var btnBg = _isDark
                ? (SolidColorBrush)FindResource("BgNavActive")
                : (SolidColorBrush)FindResource("BgCardHover");
            var btnFg = (SolidColorBrush)FindResource("AccentSecFg");

            var row = new Border
            {
                Background = bgRow,
                BorderBrush = rowBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 12, 18, 12),
                Margin = new Thickness(0, 5, 0, 0)
            };
            row.MouseEnter += (_, _) => row.Background = rowHover;
            row.MouseLeave += (_, _) => row.Background = bgRow;

            if (!_isDark)
                row.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#1A2236"),
                    BlurRadius = 16,
                    ShadowDepth = 1,
                    Opacity = 0.07,
                    Direction = 270
                };

            var g = new Grid();
            int[] widths = { 50, -1, 150, 110, 80, 90 };
            foreach (var w in widths)
                g.ColumnDefinitions.Add(w == -1
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w) });

            void Add(int col, UIElement el) { Grid.SetColumn(el, col); g.Children.Add(el); }

            var numBd = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(10),
                Background = bgNum,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numBd.Child = new TextBlock
            {
                Text = idx.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = textSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Add(0, numBd);

            var assessmentPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            assessmentPanel.Children.Add(new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Background = (Brush)FindResource("AccentSecBg"),
                Margin = new Thickness(0, 0, 9, 0),
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("AccentSecFg"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            assessmentPanel.Children.Add(new TextBlock
            {
                Text = LanguageManager.T("Cognitive assessment"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                VerticalAlignment = VerticalAlignment.Center
            });
            Add(1, assessmentPanel);
            Add(2, new TextBlock
            {
                Text = FormatRecordedAt(t.RecordedAt, t.DateTime),
                FontSize = 13,
                Foreground = textSec,
                VerticalAlignment = VerticalAlignment.Center
            });
            Add(3, new TextBlock
            {
                Text = t.Duration,
                FontSize = 13,
                Foreground = textSec,
                VerticalAlignment = VerticalAlignment.Center
            });

            Add(4, BuildScoreBadge(t.Scor));

            var btn = new Button
            {
                Content = LanguageManager.T("Details"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = btnBg,
                Foreground = btnFg,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(10, 4, 10, 4)
            };
            var tpl = new ControlTemplate(typeof(Button));
            var fef = new FrameworkElementFactory(typeof(Border));
            fef.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
            fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 4, 10, 4));
            fef.AppendChild(cp);
            tpl.VisualTree = fef;
            btn.Template = tpl;

            var captured = t;
            btn.Click += async (_, _) =>
            {
                if (!CanAccessSelectedPatientHistory())
                {
                    MessageBox.Show(LanguageManager.T("Select a patient to view test history"),
                        "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    TabPacienti.IsChecked = true;
                    return;
                }

                string uid = GetActivePatientId();
                string token = GetReg("IdToken");
                string json = await _http.GetStringAsync(
                    $"{_dbUrl}/patients/{uid}/testResults/{captured.Id}.json?auth={token}", _cts.Token);
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/testResults/{uid}/{captured.Id}.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/tests/{captured.Id}.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/testResults/{captured.Id}.json?auth={token}", _cts.Token);
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var win = new TestDetailsWindow(
                    root.TryGetProperty("map", out var m) ? m.GetString() ?? "" : "",
                    root.TryGetProperty("ecg", out var e) ? e.GetString() ?? "" : "",
                    root.TryGetProperty("spo2", out var s) ? s.GetString() ?? "" : "",
                    root.TryGetProperty("hr", out var h) ? h.GetString() ?? "" : "",
                    root.TryGetProperty("dist", out var d) ? d.GetString() ?? "" : "",
                    root.TryGetProperty("cpt", out var cpt) ? cpt.GetRawText() : ""
                );
                win.Show();
            };
            Add(5, btn);

            row.Child = g;
            return row;
        }

        private void ShowTestLoading()
        {
            TestLoadingState.Visibility = Visibility.Visible;
            TestEmptyState.Visibility = Visibility.Collapsed;
            TestTableHeader.Visibility = Visibility.Collapsed;
            TestRowsPanel.Children.Clear();
        }

        private void ShowTestEmpty()
        {
            TestLoadingState.Visibility = Visibility.Collapsed;
            TestEmptyState.Visibility = Visibility.Visible;
            TestTableHeader.Visibility = Visibility.Collapsed;
        }

        private async void RefreshTests_Click(object sender, RoutedEventArgs e)
            => await LoadTestsFromFirebaseAsync();

        public async Task LoadActivitiesFromFirebaseAsync()
        {
            if (_isLoadingActivities) return;
            _isLoadingActivities = true;

            try
            {
                string uid = GetActivePatientId();
                string token = GetReg("IdToken");

                if (!CanAccessSelectedPatientHistory())
                {
                    Dispatcher.Invoke(ShowActivitiesEmpty);
                    return;
                }

                Dispatcher.Invoke(ShowActivitiesLoading);

                string json = await _http.GetStringAsync(
                    $"{_dbUrl}/patients/{uid}/activityResults.json?auth={token}", _cts.Token);
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/activityResults/{uid}.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/activities.json?auth={token}", _cts.Token);
                }
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = await _http.GetStringAsync(
                        $"{_dbUrl}/{uid}/activityResults.json?auth={token}", _cts.Token);
                }

                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    Dispatcher.Invoke(() => { ShowActivitiesEmpty(); UpdateProfileStats(_testsCache, new()); });
                    return;
                }

                var activities = ParseActivities(json);
                Dispatcher.Invoke(() => RenderActivities(activities));
            }
            catch
            {
                Dispatcher.Invoke(ShowActivitiesEmpty);
            }
            finally
            {
                _isLoadingActivities = false;
            }
        }

        private List<ActivityEntry> ParseActivities(string json)
        {
            var list = new List<ActivityEntry>();
            using var doc = JsonDocument.Parse(json);

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var v = entry.Value;
                string dt = v.TryGetProperty("dateTime", out var dtv) ? dtv.GetString() ?? "" : "";
                string dur = v.TryGetProperty("duration", out var dv) ? dv.GetString() ?? "" : "";
                string game = v.TryGetProperty("game", out var gv) ? gv.GetString() ?? "" : "";
                string difficulty = v.TryGetProperty("difficulty", out var dfv) ? dfv.GetString() ?? "" : "";
                double scor = v.TryGetProperty("scor", out var sv) ? sv.GetDouble() : 0;
                list.Add(new ActivityEntry(entry.Name, dt, ParseRecordedAt(dt), dur, game, difficulty, scor));
            }

            return list.OrderByDescending(a => a.RecordedAt).ToList();
        }

        private void RenderActivities(List<ActivityEntry> activities)
        {
            _activitiesCache = activities;
            ActRowsPanel.Children.Clear();

            if (activities.Count == 0)
            {
                ShowActivitiesEmpty();
                UpdateProfileStats(_testsCache, activities);
                return;
            }

            ActLoadingState.Visibility = Visibility.Collapsed;
            ActEmptyState.Visibility = Visibility.Collapsed;
            ActTableHeader.Visibility = Visibility.Visible;

            var last = activities.First();
            ActLastDate.Text = FormatRecordedAt(last.RecordedAt, last.DateTime);
            ActBestScore.Text = FormatScore(activities.Max(a => a.Scor));
            ActAvgScore.Text = FormatScore(activities.Average(a => a.Scor));
            ActTotalSessions.Text = activities.Count.ToString();

            for (int i = 0; i < activities.Count; i++)
                ActRowsPanel.Children.Add(BuildActivityRow(i + 1, activities[i]));

            UpdateProfileStats(_testsCache, activities);
            LanguageManager.Apply(this);
        }

        private void ShowActivitiesLoading()
        {
            ActLoadingState.Visibility = Visibility.Visible;
            ActEmptyState.Visibility = Visibility.Collapsed;
            ActTableHeader.Visibility = Visibility.Collapsed;
            ActRowsPanel.Children.Clear();
        }

        private void ShowActivitiesEmpty()
        {
            ActLoadingState.Visibility = Visibility.Collapsed;
            ActEmptyState.Visibility = Visibility.Visible;
            ActTableHeader.Visibility = Visibility.Collapsed;
            ActRowsPanel.Children.Clear();
        }

        private Border BuildActivityRow(int idx, ActivityEntry a)
        {
            var bgRow = (SolidColorBrush)FindResource("RowBg");
            var bgNum = (SolidColorBrush)FindResource("RowNumBg");
            var rowHover = (SolidColorBrush)FindResource("RowHover");
            var rowBorder = (SolidColorBrush)FindResource("RowBorder");
            var textPri = (SolidColorBrush)FindResource("TxtPrimary");
            var textSec = (SolidColorBrush)FindResource("TxtSecondary");

            var (diffBgHex, diffFgHex) = GetDifficultyPalette(a.Difficulty);

            string gameIcon = a.Game switch
            {
                "Memory" or "Memorie" => "🧠",
                "Stroop Test" => "🎨",
                "Visual Search" => "🔍",
                "Sequences" or "Secvențe" => "🔢",
                "Quick Math" or "Matematică rapidă" => "➕",
                _ => "🎮"
            };

            var row = new Border
            {
                Background = bgRow,
                BorderBrush = rowBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 12, 18, 12),
                Margin = new Thickness(0, 5, 0, 0)
            };
            row.MouseEnter += (_, _) => row.Background = rowHover;
            row.MouseLeave += (_, _) => row.Background = bgRow;

            if (!_isDark)
                row.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#1A2236"),
                    BlurRadius = 16,
                    ShadowDepth = 1,
                    Opacity = 0.07,
                    Direction = 270
                };

            var g = new Grid();
            int[] widths = { 46, -1, 150, 110, 140, 110 };
            foreach (var w in widths)
                g.ColumnDefinitions.Add(w == -1
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w) });

            void Add(int col, UIElement el) { Grid.SetColumn(el, col); g.Children.Add(el); }

            var numBd = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(10),
                Background = bgNum,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numBd.Child = new TextBlock
            {
                Text = idx.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = textSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Add(0, numBd);

            var gamePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 24, 0)
            };
            gamePanel.Children.Add(new TextBlock
            {
                Text = gameIcon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("TxtPrimary")
            });
            gamePanel.Children.Add(new TextBlock
            {
                Text = LanguageManager.T(a.Game),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                VerticalAlignment = VerticalAlignment.Center
            });
            Add(1, gamePanel);

            Add(2, new TextBlock
            {
                Text = FormatRecordedAt(a.RecordedAt, a.DateTime),
                FontSize = 13,
                Foreground = textSec,
                VerticalAlignment = VerticalAlignment.Center
            });

            Add(3, new TextBlock
            {
                Text = a.Duration,
                FontSize = 13,
                Foreground = textSec,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (!string.IsNullOrEmpty(a.Difficulty))
            {
                var diffBd = new Border
                {
                    Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(diffBgHex)),
                    BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(diffFgHex)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                diffBd.Child = new TextBlock
                {
                    Text = LanguageManager.T(a.Difficulty),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(diffFgHex))
                };
                Add(4, diffBd);
            }

            Add(5, BuildScoreBadge(a.Scor));

            row.Child = g;
            return row;
        }

        private async void RefreshActivities_Click(object sender, RoutedEventArgs e)
            => await LoadActivitiesFromFirebaseAsync();

        private void UpdateProfileStats(List<TestEntry> tests, List<ActivityEntry> activities)
        {
            UpdateDoctorStats(_patientsCache);
        }

        private void NewGame_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePatientHistoryUnlocked(LanguageManager.T("Select a patient to view activity history"))) return;

            var gameSelection = new GameSelectionWindow(this, _isDark);
            gameSelection.Show();
        }

        public void StartGameAndRefresh(Window gameWindow)
        {
            gameWindow.Owner = this;
            gameWindow.Closed += (s, args) =>
            {
                RefreshActivitiesAfterGame();
            };
            gameWindow.Show();
        }

        public async void RefreshActivitiesAfterGame()
        {
            await LoadActivitiesFromFirebaseAsync();
            _ = LoadPatientsFromFirebaseAsync();
        }

        private async Task LoadPatientsFromFirebaseAsync()
        {
            if (_isLoadingPatients) return;
            _isLoadingPatients = true;

            try
            {
                string doctorId = GetReg("Uid");
                string token = GetReg("IdToken");
                string email = GetReg("Email");
                if (string.IsNullOrEmpty(doctorId))
                {
                    Dispatcher.Invoke(ShowPatientsEmpty);
                    return;
                }

                Dispatcher.Invoke(ShowPatientsLoading);

                var patientIds = await LoadAssignedPatientIdsAsync(doctorId, token, email);
                var patients = new List<PatientEntry>();

                foreach (var patientId in patientIds.Distinct())
                {
                    var profile = await LoadPatientProfileAsync(patientId, token);
                    var tests = await LoadPatientTestsAsync(patientId, token);
                    var activities = await LoadPatientActivitiesAsync(patientId, token);
                    var prediction = await BuildPredictionAsync(patientId, tests, activities);

                    string name = BuildPatientName(profile, patientId);
                    string patientEmail = profile.TryGetValue("email", out var em) ? em : "";
                    string phone = profile.TryGetValue("phone-number", out var ph) ? ph :
                                   profile.TryGetValue("phone", out var ph2) ? ph2 : "";

                    double totalTestScore = tests.Sum(t => t.Scor);
                    double totalActivityScore = activities.Sum(a => a.Scor);
                    patients.Add(new PatientEntry(patientId, name, patientEmail, phone,
                        tests.Count, totalTestScore,
                        activities.Count, totalActivityScore,
                        prediction));
                }

                Dispatcher.Invoke(() => RenderPatients(patients.OrderBy(p => p.Name).ToList()));
            }
            catch
            {
                Dispatcher.Invoke(ShowPatientsEmpty);
            }
            finally
            {
                _isLoadingPatients = false;
            }
        }

        private async Task<List<string>> LoadAssignedPatientIdsAsync(string doctorId, string token, string email)
        {
            var ids = new List<string>();

            string json = await SafeGetAsync($"{_dbUrl}/doctors/{doctorId}/patients.json?auth={token}");
            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.EnumerateObject())
                    ids.Add(item.Name);
            }

            string patientsJson = await SafeGetAsync($"{_dbUrl}/patients.json?auth={token}");
            if (!string.IsNullOrEmpty(patientsJson) && patientsJson != "null")
            {
                using var doc = JsonDocument.Parse(patientsJson);
                foreach (var patient in doc.RootElement.EnumerateObject())
                {
                    if (!patient.Value.TryGetProperty("doctorId", out var doctorIdValue)) continue;
                    string assignedDoctorId = doctorIdValue.GetString() ?? "";
                    if (assignedDoctorId == doctorId)
                        ids.Add(patient.Name);
                }
            }

            string legacyJson = await SafeGetAsync($"{_dbUrl}.json?auth={token}");
            if (!string.IsNullOrEmpty(legacyJson) && legacyJson != "null")
            {
                using var doc = JsonDocument.Parse(legacyJson);
                foreach (var user in doc.RootElement.EnumerateObject())
                {
                    if (user.Value.TryGetProperty("doctorId", out var directDoctorId) &&
                        directDoctorId.GetString() == doctorId)
                    {
                        ids.Add(user.Name);
                        continue;
                    }

                    if (!user.Value.TryGetProperty("profile", out var profile)) continue;
                    string docEmail = profile.TryGetProperty("doctor-email", out var de) ? de.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(email) &&
                        string.Equals(docEmail, email, StringComparison.OrdinalIgnoreCase))
                    {
                        ids.Add(user.Name);
                    }
                }
            }

            return ids;
        }

        private async Task<Dictionary<string, string>> LoadPatientProfileAsync(string patientId, string token)
        {
            string json = await SafeGetAsync($"{_dbUrl}/patients/{patientId}.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/patients/{patientId}/profile.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/profile.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}.json?auth={token}");

            var profile = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json) || json == "null") return profile;

            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    profile[prop.Name] = prop.Value.GetString() ?? "";
            }
            return profile;
        }

        private async Task<List<TestEntry>> LoadPatientTestsAsync(string patientId, string token)
        {
            string json = await SafeGetAsync($"{_dbUrl}/patients/{patientId}/testResults.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/testResults/{patientId}.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/tests.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/testResults.json?auth={token}");

            return string.IsNullOrEmpty(json) || json == "null" ? new List<TestEntry>() : ParseTests(json);
        }

        private async Task<List<ActivityEntry>> LoadPatientActivitiesAsync(string patientId, string token)
        {
            string json = await SafeGetAsync($"{_dbUrl}/patients/{patientId}/activityResults.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/activityResults/{patientId}.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/activities.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/activityResults.json?auth={token}");

            return string.IsNullOrEmpty(json) || json == "null" ? new List<ActivityEntry>() : ParseActivities(json);
        }

        private static string BuildPatientName(Dictionary<string, string> profile, string fallback)
        {
            profile.TryGetValue("name", out var name);
            profile.TryGetValue("surname", out var surname);
            string fullName = $"{name} {surname}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;
        }

        private async Task<PredictionSummary> BuildPredictionAsync(string patientId, List<TestEntry> tests, List<ActivityEntry> activities)
        {
            var history = tests
                .Select(t => t.Features)
                .Where(f => f != null && f.DateTime != DateTime.MinValue)
                .Cast<SessionFeatures>()
                .OrderBy(f => f.DateTime)
                .ToList();

            if (history.Count < 2)
                return new PredictionSummary("insufficient", "At least 2 valid test sessions are required for prediction.");

            try
            {
                PredictionEngineResolution resolution = await PredictionModelService.GetEngineAsync(history, "patient:" + patientId);
                PredictionResult result = resolution.Engine.Predict(history);
                string direction = result.Trend switch
                {
                    TrendDirection.Improving => "positive",
                    TrendDirection.Declining => "negative",
                    _ => "stable"
                };

                string intervalText = result.IntervalStatusLabel switch
                {
                    "Empirical 90% interval" => $"Empirical 90% prediction interval: {result.PredictionIntervalLow:F1}-{result.PredictionIntervalHigh:F1}",
                    "Rolling-origin empirical interval" => $"Rolling-origin empirical interval (90% nominal): {result.PredictionIntervalLow:F1}-{result.PredictionIntervalHigh:F1}",
                    _ => "Prediction interval is not calibrated"
                };
                if (result.TestIntervalCoverage90.HasValue)
                    intervalText += $"; measured holdout coverage: {result.TestIntervalCoverage90.Value:P0}";

                string reason =
                    $"Predicted next test score: {result.PredictedScore:F1}. " +
                    $"Model: {result.ModelName}. {intervalText}. " +
                    $"Trend: {result.TrendLabel} ({result.TrendPerSession:+0.##;-0.##;0} points/session). " +
                    $"Historical test MAE: {(result.ModelMAE.HasValue ? result.ModelMAE.Value.ToString("F1") : "not available")}. " +
                    $"Data: {result.DataQualityLabel}";

                if (resolution.TrainingReport is { Success: false } report)
                    reason += $" Training status: {report.Message}";

                if (!string.IsNullOrWhiteSpace(result.AlertMessage))
                    reason += $" Alerts: {result.AlertMessage}";

                return new PredictionSummary(direction, reason);
            }
            catch
            {
                return BuildFallbackPrediction(tests, activities);
            }
        }

        private static PredictionSummary BuildFallbackPrediction(List<TestEntry> tests, List<ActivityEntry> activities)
        {
            double[] scores = tests.Select(t => t.Scor).Where(s => double.IsFinite(s) && s >= 0 && s <= 100).ToArray();
            if (scores.Length < 2)
                return new PredictionSummary("insufficient", "At least 2 valid test sessions are required.");

            double latest = scores[^1];
            double prior = scores[^2];
            double delta = latest - prior;
            if (delta >= 3)
                return new PredictionSummary("positive", $"The latest test score is {delta:F1} points higher than the previous test. This is a simple fallback, not a calibrated prediction.");
            if (delta <= -3)
                return new PredictionSummary("negative", $"The latest test score is {Math.Abs(delta):F1} points lower than the previous test. This is a simple fallback, not a calibrated prediction.");
            return new PredictionSummary("stable", "The latest test score is close to the previous test. This is a simple fallback, not a calibrated prediction.");
        }

        private void RenderPatients(List<PatientEntry> patients)
        {
            _patientsCache = patients;
            PatientsRowsPanel.Children.Clear();

            PatientsTotal.Text = patients.Count.ToString();
            PatientsPositive.Text = patients.Count(p => p.Prediction.Direction == "positive").ToString();
            PatientsAttention.Text = patients.Count(p => p.Prediction.Direction == "negative").ToString();

            UpdateDoctorStats(patients);

            if (patients.Count == 0)
            {
                ShowPatientsEmpty();
                return;
            }

            PatientsLoadingState.Visibility = Visibility.Collapsed;
            PatientsEmptyState.Visibility = Visibility.Collapsed;
            PatientsTableHeader.Visibility = Visibility.Visible;

            for (int i = 0; i < patients.Count; i++)
                PatientsRowsPanel.Children.Add(BuildPatientRow(i + 1, patients[i]));

            LanguageManager.Apply(this);
        }

        private void UpdateDoctorStats(List<PatientEntry> patients)
        {
            if (StatNrPacienti == null) return;

            int patientCount = patients.Count;
            int testCount = patients.Sum(p => p.TestCount);
            int activityCount = patients.Sum(p => p.ActivityCount);

            StatNrPacienti.Text = patientCount.ToString();
            StatNrTestari.Text = patientCount > 0 ? FormatAverage(patients.Average(p => p.TestCount)) : "0";
            StatNrActivitati.Text = patientCount > 0 ? FormatAverage(patients.Average(p => p.ActivityCount)) : "0";
            StatScorMediu.Text = testCount > 0
                ? FormatAverage(patients.Sum(p => p.TotalTestScore) / testCount)
                : "—";
            StatScorMediuActivitati.Text = activityCount > 0
                ? FormatAverage(patients.Sum(p => p.TotalActivityScore) / activityCount)
                : "—";
        }

        private static string FormatAverage(double value)
            => value % 1 == 0 ? value.ToString("F0") : value.ToString("F1");

        private Border BuildPatientRow(int idx, PatientEntry patient)
        {
            var bgRow = (SolidColorBrush)FindResource("RowBg");
            var rowHover = (SolidColorBrush)FindResource("RowHover");
            var rowBorder = (SolidColorBrush)FindResource("RowBorder");
            var bgNum = (SolidColorBrush)FindResource("RowNumBg");
            var textPri = (SolidColorBrush)FindResource("TxtPrimary");
            var textSec = (SolidColorBrush)FindResource("TxtSecondary");
            var btnBg = _isDark ? (SolidColorBrush)FindResource("BgNavActive") : (SolidColorBrush)FindResource("BgCardHover");
            var btnFg = (SolidColorBrush)FindResource("AccentSecFg");

            var row = new Border
            {
                Background = bgRow,
                BorderBrush = rowBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(18, 13, 18, 13),
                Margin = new Thickness(0)
            };
            row.MouseEnter += (_, _) => row.Background = rowHover;
            row.MouseLeave += (_, _) => row.Background = bgRow;

            var g = new Grid();
            
            int[] widths = { 46, 1, 100, 100, 140, 110 }; 
            foreach (var w in widths)
                g.ColumnDefinitions.Add(w == 1
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w) });

            void Add(int col, UIElement el) { Grid.SetColumn(el, col); g.Children.Add(el); }

            var numBd = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(10),
                Background = bgNum,
                VerticalAlignment = VerticalAlignment.Center
            };
            numBd.Child = new TextBlock
            {
                Text = idx.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = textSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Add(0, numBd);

            var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new TextBlock
            {
                Text = patient.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(patient.Phone) ? patient.Id : patient.Phone,
                FontSize = 11,
                Foreground = textSec
            });
            Add(1, namePanel);

            Add(2, BuildCenteredText(patient.TestCount.ToString(), textPri));
            
            Add(3, BuildCenteredText(patient.ActivityCount.ToString(), textPri));

            Add(4, BuildPredictionBadge(patient.Prediction));

            var selectBtn = new Button
            {
                Content = patient.Id == _selectedPatientId ? LanguageManager.T("Selected") : LanguageManager.T("Details"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = btnBg,
                Foreground = btnFg,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(10, 4, 10, 4)
            };
            
            var tpl = new ControlTemplate(typeof(Button));
            var fef = new FrameworkElementFactory(typeof(Border));
            fef.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
            fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 4, 10, 4));
            fef.AppendChild(cp);
            tpl.VisualTree = fef;
            selectBtn.Template = tpl;

            selectBtn.Click += async (_, _) =>
            {
                SelectPatient(patient);
                await LoadTestsFromFirebaseAsync();
                await LoadActivitiesFromFirebaseAsync();
                TabTestari.IsChecked = true;
            };
            Add(5, selectBtn);

            row.Child = g;
            return row;
        }
        private static TextBlock BuildCenteredText(string text, Brush foreground) => new()
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        private Border BuildPredictionBadge(PredictionSummary prediction)
        {
            var (bg, fg) = prediction.Direction switch
            {
                "positive" => (_isDark ? "#183B2A" : "#DCFCE7", _isDark ? "#4ADE80" : "#15803D"),
                "negative" => (_isDark ? "#451A1A" : "#FEE2E2", _isDark ? "#F87171" : "#B91C1C"),
                "stable" => (_isDark ? "#172554" : "#DBEAFE", _isDark ? "#60A5FA" : "#1D4ED8"),
                _ => (_isDark ? "#1E293B" : "#F1F5F9", _isDark ? "#CBD5E1" : "#475569")
            };

            var badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = BuildPredictionTooltip(prediction)
            };
            badge.Child = new TextBlock
            {
                Text = LanguageManager.T(prediction.Direction),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg))
            };
            return badge;
        }

        private static string BuildPredictionTooltip(PredictionSummary prediction)
        {
            if (LanguageManager.Current != AppLanguage.Romanian)
                return prediction.Reason;

            return prediction.Direction switch
            {
                "positive" => "Tendință pozitivă estimată pe baza rezultatelor din sesiunile anterioare.",
                "negative" => "Tendință descendentă estimată pe baza rezultatelor din sesiunile anterioare.",
                "stable" => "Rezultatele recente indică o tendință stabilă.",
                _ => "Sunt necesare cel puțin două sesiuni de test valide pentru a calcula o estimare."
            };
        }

        private void SelectPatient(PatientEntry patient)
        {
            _selectedPatientId = patient.Id;
            _selectedPatientName = patient.Name;
            SelectedPatientTestsLabel.Text = $"{LanguageManager.T("Selected patient:")} {patient.Name}";
            SelectedPatientActivitiesLabel.Text = $"{LanguageManager.T("Selected patient:")} {patient.Name}";
            _testsCache.Clear();
            _activitiesCache.Clear();
            ShowTestEmpty();
            ShowActivitiesEmpty();
            RenderPatients(_patientsCache);
        }

        private void OpenTrendAnalysisForCurrentPatient_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePatientHistoryUnlocked(LanguageManager.T("Select a patient to view trend analysis"))) return;
            var patient = _patientsCache.FirstOrDefault(p => p.Id == GetActivePatientId());
            if (patient != null)
                OpenTrendAnalysis(patient);
        }

        private void OpenTrendAnalysis(PatientEntry patient)
        {
            var trendWindow = new TrendAnalysisWindow(patient.Id, patient.Name, _isDark);
            trendWindow.Owner = this;
            trendWindow.ShowDialog();
        }

        private bool EnsurePatientSelected()
        {
            if (!string.IsNullOrWhiteSpace(_selectedPatientId)) return true;

            MessageBox.Show(LanguageManager.T("Select a patient from the patient list first."),
                "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
            TabPacienti.IsChecked = true;
            return false;
        }

        private bool EnsurePatientHistoryUnlocked(string message)
        {
            if (CanAccessSelectedPatientHistory()) return true;

            MessageBox.Show(message, "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
            TabPacienti.IsChecked = true;
            return false;
        }

        private bool CanAccessSelectedPatientHistory()
        {
            return !string.IsNullOrWhiteSpace(_selectedPatientId);
        }

        private void ShowPatientsLoading()
        {
            PatientsLoadingState.Visibility = Visibility.Visible;
            PatientsEmptyState.Visibility = Visibility.Collapsed;
            PatientsTableHeader.Visibility = Visibility.Collapsed;
            PatientsRowsPanel.Children.Clear();
        }

        private void ShowPatientsEmpty()
        {
            PatientsLoadingState.Visibility = Visibility.Collapsed;
            PatientsEmptyState.Visibility = Visibility.Visible;
            PatientsTableHeader.Visibility = Visibility.Collapsed;
            PatientsRowsPanel.Children.Clear();
        }

        private async void RefreshPatients_Click(object sender, RoutedEventArgs e)
            => await LoadPatientsFromFirebaseAsync();

        private async Task<string> SafeGetAsync(string url)
        {
            try { return await _http.GetStringAsync(url, _cts.Token); }
            catch { return ""; }
        }

        private string GetActivePatientId()
        {
            if (!string.IsNullOrWhiteSpace(_selectedPatientId)) return _selectedPatientId;
            return FocusSession.ActivePatientId;
        }

        private string GetReg(string key)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RegPath);
                string value = k?.GetValue(key)?.ToString() ?? "";
                return key == "IdToken" ? SessionTokenProtector.Unprotect(value) : value;
            }
            catch { return ""; }
        }

        private void Dashboard_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            LanguageManager.LanguageChanged -= Dashboard_LanguageChanged;
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("LoggedIn", "0");
                key.SetValue("IdToken", "");
                key.SetValue("RememberMe", "0");
                key.SetValue("Email", "");
                key.SetValue("Uid", "");
                key.SetValue("ActivePatientId", "");
                key.SetValue("Name", "");
                key.SetValue("Surname", "");
            }
            catch { }

            new Login().Show();
            Close();
        }
    }

    public class ProfileData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }

        [JsonPropertyName("birth-date")]
        public string? LegacyBirthDate { get; set; }

        [JsonPropertyName("birthDate")]
        public string? BirthDate { get; set; }

        [JsonPropertyName("phone-number")]
        public string? LegacyPhone { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("doctor-email")]
        public string? DoctorEmail { get; set; }

        [JsonPropertyName("adress")]
        public string? Adress { get; set; }

        public string EffectiveBirthDate => BirthDate ?? LegacyBirthDate ?? "";
        public string EffectivePhone => Phone ?? LegacyPhone ?? "";
    }
}
