using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class StartTest : Window
    {
        private readonly Dashboard _dashboard;
        private readonly bool _isDark;
        private const string RegPath = @"Software\FocusAI";
        private bool _done1, _done2, _done3;

        private readonly DispatcherTimer _timer = new();
        private TimeSpan _elapsed = TimeSpan.Zero;
        private bool _timerStarted = false;

        private static readonly Color CardDoneBg = Color.FromRgb(14, 30, 14);
        private static readonly Color CardDoneBorder = Color.FromRgb(34, 197, 94);

        private static readonly string EyeTrackerDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         @"..\..\..\..\EyeTracker-main\Webcam3DTracker"));
        private const string PythonScript = "MonitorTracking.py";
        private const string ArduinoPort = "COM12";

        private readonly string _dbUrl =
            ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private readonly string _sendGridApiKey =
            ConfigurationManager.AppSettings["SendGridApiKey"] ?? "";
        private readonly string _sendGridEmail =
            ConfigurationManager.AppSettings["SendGridEmail"] ?? "";

        private string _mapData = "";
        private double _reactionTimeSec = 0;
        private double _goNoGoAccuracy = 0;

        private static readonly HttpClient _http = new();

        public StartTest(Dashboard dashboard, bool isDark)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            _dashboard = dashboard;
            _isDark = isDark;

            ThemeManager.Apply(_isDark);
            RefreshUI();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            Loaded += StartTest_Loaded;
            Closing += StartTest_Closing;
        }

        private void StartTest_Loaded(object sender, RoutedEventArgs e)
        {
            BioCollector.Instance.TryOpen(ArduinoPort);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
            TimerText.Text = _elapsed.ToString(@"mm\:ss");
        }

        private void EnsureTimerStarted()
        {
            if (_timerStarted) return;

            _timerStarted = true;
            _timer.Start();
            TimerStatusText.Text = "În desfășurare";
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

            BioCollector.Instance.StartStreaming(reset: true);
        }

        private void StopTimer()
        {
            _timer.Stop();
            TimerStatusText.Text = "Finalizat";
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
        }

        private void StartStep1_Click(object sender, RoutedEventArgs e) => RunStep(1);
        private void StartStep2_Click(object sender, RoutedEventArgs e) => RunStep(2);
        private void StartStep3_Click(object sender, RoutedEventArgs e) => RunStep(3);

        private async void RunStep(int stepIndex)
        {
            EnsureTimerStarted();
            Hide();

            try
            {
                bool stepCompleted = await LaunchStepWindowAsync(stepIndex);
                if (stepCompleted)
                {
                    MarkStepDone(stepIndex);
                    if (_done1 && _done2 && _done3)
                    {
                        StopTimer();
                        BioCollector.Instance.StopStreaming();
                        Show();
                        await ShowCompletionMessageAsync();
                        return;
                    }
                }
            }
            finally
            {
                if (!(_done1 && _done2 && _done3))
                    Show();
            }
        }

        private async Task<bool> LaunchStepWindowAsync(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1:
                    return await RunEyeTrackerAsync();

                case 2:
                    var buzzerWin = new BuzzerTest(_isDark);
                    buzzerWin.ShowDialog();
                    if (buzzerWin.ReactionTime.HasValue)
                        _reactionTimeSec = buzzerWin.ReactionTime.Value;
                    return true;

                case 3:
                    var goNoGoWin = new GoNoGoTest(_isDark);
                    goNoGoWin.ShowDialog();
                    _goNoGoAccuracy = goNoGoWin.Accuracy;
                    return true;

                default:
                    return false;
            }
        }

        private async Task<bool> RunEyeTrackerAsync()
        {
            string scriptPath = Path.Combine(EyeTrackerDir, PythonScript);

            if (!Directory.Exists(EyeTrackerDir))
            {
                MessageBox.Show($"Directorul eye-tracker nu a fost găsit:\n{EyeTrackerDir}",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Scriptul Python nu a fost găsit:\n{scriptPath}",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{PythonScript}\"",
                WorkingDirectory = EyeTrackerDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using var process = new Process { StartInfo = psi };
                process.ErrorDataReceived += (_, _) => { };
                process.Start();
                process.BeginErrorReadLine();

                string stdoutData = await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                if (!string.IsNullOrWhiteSpace(stdoutData))
                {
                    _mapData = stdoutData.Trim();
                    return true;
                }

                MessageBox.Show("Scriptul nu a returnat coordonate.\nVerifică eye tracker-ul.",
                    "Focus AI – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                MessageBox.Show("Python nu a fost găsit în PATH.\n\n" + ex.Message,
                    "Eroare – Python lipsă", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare neașteptată:\n{ex.Message}",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void MarkStepDone(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1: _done1 = true; break;
                case 2: _done2 = true; break;
                case 3: _done3 = true; break;
            }
            RefreshUI();
        }

        private void RefreshUI()
        {
            UpdateCard(Card1Border, Status1Badge, Status1Text, StartBtn1, StartBtn1Text, _done1);
            UpdateCard(Card2Border, Status2Badge, Status2Text, StartBtn2, StartBtn2Text, _done2);
            UpdateCard(Card3Border, Status3Badge, Status3Text, StartBtn3, StartBtn3Text, _done3);

            var green = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            var gray = new SolidColorBrush(Color.FromRgb(55, 65, 81));
            ProgDot1.Fill = _done1 ? green : gray;
            ProgDot2.Fill = _done2 ? green : gray;
            ProgDot3.Fill = _done3 ? green : gray;

            int doneCount = (_done1 ? 1 : 0) + (_done2 ? 1 : 0) + (_done3 ? 1 : 0);
            ProgressText.Text = $"{doneCount} / 3 etape finalizate";
        }

        private void UpdateCard(
            Border border, Border statusBadge, TextBlock statusText,
            Button startBtn, TextBlock startBtnText,
            bool done)
        {
            if (done)
            {
                border.Background = new SolidColorBrush(CardDoneBg);
                border.BorderBrush = new SolidColorBrush(CardDoneBorder);
                border.BorderThickness = new Thickness(1.5);

                statusBadge.Background = new SolidColorBrush(Color.FromRgb(20, 83, 45));
                statusText.Text = "✓  Finalizată";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));

                startBtn.IsEnabled = false;
                startBtnText.Text = "Finalizată";
            }
            else
            {
                border.Background = (SolidColorBrush)FindResource("BgCard");
                border.BorderBrush = (SolidColorBrush)FindResource("BgSep");
                border.BorderThickness = new Thickness(1.5);

                statusBadge.Background = (SolidColorBrush)FindResource("BgNavActive");
                statusText.Text = "Neparcursă";
                statusText.Foreground = (SolidColorBrush)FindResource("TxtMuted");

                startBtn.IsEnabled = true;
                startBtnText.Text = "▶  Start etapă";
            }
        }

        private async Task ShowCompletionMessageAsync()
        {
            string time = _elapsed.ToString(@"mm\:ss");

            string summary =
                $"🎉 Toate etapele au fost finalizate!\n\n" +
                $"Timp total: {time}\n" +
                $"Dorești să salvezi rezultatele în Firebase?";

            var result = MessageBox.Show(summary, "Test finalizat",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                await SaveResultsAsync();

            _dashboard.Show();
            Close();
        }

        private double ComputeScore(string mapStr, double reactionSec,
                                    double goNoGoAcc, string distStr)
        {
            double mapScore = 0;
            if (!string.IsNullOrWhiteSpace(mapStr))
            {
                var points = mapStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                int total = points.Length;
                int inRange = 0;

                foreach (var pt in points)
                {
                    var parts = pt.Split(',');
                    if (parts.Length == 2
                        && double.TryParse(parts[0], out double x)
                        && double.TryParse(parts[1], out double y))
                    {
                        if (x >= 0 && x <= 100 && y >= 0 && y <= 100)
                            inRange++;
                    }
                }

                mapScore = total > 0 ? (double)inRange / total * 45.0 : 0;
            }

            double rtScore = 0;
            if (reactionSec > 0)
                rtScore = Math.Min(1.0 / reactionSec * 25.0, 25.0);

            double goNoGoScore = goNoGoAcc / 100.0 * 25.0;

            int distZeroCount = 0;
            if (!string.IsNullOrWhiteSpace(distStr))
            {
                distZeroCount = distStr.Split(',')
                    .Count(v => v.Trim() == "0");
            }
            double penalty = 5.0 * distZeroCount;

            double score = mapScore + rtScore + goNoGoScore - penalty;

            return Math.Round(Math.Max(0, Math.Min(100, score)), 2);
        }

        private async Task SaveResultsAsync()
        {
            var bio = BioCollector.Instance;

            string ecgStr = string.Join(";", bio.Ecg.Select(s => $"{s.EcgDreapta},{s.EcgStanga}"));
            string hrStr = string.Join(",", bio.HeartRate.Where(v => v != 0));
            string spo2Str = string.Join(",", bio.SpO2.Where(v => v != 0));
            string distStr = string.Join(",", bio.Distance.Select(d => d ? "1" : "0"));

            double scor = ComputeScore(_mapData, _reactionTimeSec, _goNoGoAccuracy, distStr);

            string dateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string duration = _elapsed.ToString(@"mm\:ss");

            string testId = await GetNextTestIdAsync();
            string uid = GetReg("Uid");
            string json = "{"
                + $"\"dateTime\":\"{Escape(dateTime)}\","
                + $"\"duration\":\"{Escape(duration)}\","
                + $"\"map\":\"{Escape(_mapData)}\","
                + $"\"ecg\":\"{Escape(ecgStr)}\","
                + $"\"hr\":\"{Escape(hrStr)}\","
                + $"\"spo2\":\"{Escape(spo2Str)}\","
                + $"\"dist\":\"{Escape(distStr)}\","
                + $"\"tr2\":{_reactionTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},"
                + $"\"precizie_gonogo\":{_goNoGoAccuracy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},"
                + $"\"scor\":{scor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"
                + "}";

            string url = $"{_dbUrl.TrimEnd('/')}/{uid}/tests/{testId}.json";

            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    // Try to send email to doctor
                    bool emailSent = await SendEmailToDoctorAsync(uid, testId);
                    if (emailSent)
                    {
                        MessageBox.Show($"Rezultatele au fost salvate în Firebase!\nTest ID: {testId}\nUn email a fost trimis medicului.",
                            "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Rezultatele au fost salvate în Firebase!\nTest ID: {testId}\nNu s-a putut trimite emailul către medic (adresă lipsă sau eroare).",
                            "Focus AI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    string body = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Eroare Firebase ({response.StatusCode}):\n{body}",
                        "Focus AI", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la trimiterea datelor:\n{ex.Message}",
                    "Focus AI", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<bool> SendEmailToDoctorAsync(string uid, string testId)
        {
            try
            {
                // 1. Get doctor's email from Firebase
                string doctorEmailUrl = $"{_dbUrl.TrimEnd('/')}/{uid}/profile/doctor-email.json";
                var emailResponse = await _http.GetAsync(doctorEmailUrl);
                if (!emailResponse.IsSuccessStatusCode)
                    return false;

                string emailJson = await emailResponse.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(emailJson) || emailJson == "null")
                    return false;

                // Remove quotes if present
                string doctorEmail = emailJson.Trim('"');
                if (string.IsNullOrWhiteSpace(doctorEmail))
                    return false;

                // 2. Prepare SendGrid email
                if (string.IsNullOrEmpty(_sendGridApiKey) || string.IsNullOrEmpty(_sendGridEmail))
                    return false;

                string link = $"https://sitefocus.vercel.app/{uid}/{testId}";
                string subject = "Focus AI - Rezultate test pacient";
                string htmlContent = $@"
                    <html>
                    <body>
                        <h2>Focus AI</h2>
                        <p>Un pacient a finalizat testul cognitiv.</p>
                        <p><strong>ID test:</strong> {testId}</p>
                        <p><strong>Link rezultate:</strong> <a href='{link}'>{link}</a></p>
                        <p>Vă rugăm să accesați link-ul pentru a vizualiza detaliile complete.</p>
                        <hr/>
                        <small>Acest mesaj a fost generat automat de aplicația Focus AI.</small>
                    </body>
                    </html>";

                var emailPayload = new
                {
                    personalizations = new[]
                    {
                        new
                        {
                            to = new[] { new { email = doctorEmail } },
                            subject = subject
                        }
                    },
                    from = new { email = _sendGridEmail },
                    content = new[]
                    {
                        new
                        {
                            type = "text/html",
                            value = htmlContent
                        }
                    }
                };

                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(emailPayload);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sendGridApiKey);

                var sendResponse = await _http.PostAsync("https://api.sendgrid.com/v3/mail/send", httpContent);
                return sendResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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

        private async Task<string> GetNextTestIdAsync()
        {
            try
            {
                string uid = GetReg("Uid");
                string url = $"{_dbUrl.TrimEnd('/')}/{uid}/tests.json?shallow=true";
                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return "001";

                string body = await response.Content.ReadAsStringAsync();

                if (body == "null" || string.IsNullOrWhiteSpace(body))
                    return "001";

                int count = body.Split(new[] { ":true" }, StringSplitOptions.None).Length - 1;
                return (count + 1).ToString("D3");
            }
            catch
            {
                return "001";
            }
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\")
             .Replace("\"", "\\\"")
             .Replace("\r", "")
             .Replace("\n", "");

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Ești sigur că vrei să anulezi testul?\nProgresul curent nu va fi salvat.",
                "Anulare test", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                _timer.Stop();
                BioCollector.Instance.StopStreaming();
                _dashboard.Show();
                Close();
            }
        }

        private void StartTest_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            BioCollector.Instance.Close();
            if (!_dashboard.IsVisible)
                _dashboard.Show();
        }
    }
}