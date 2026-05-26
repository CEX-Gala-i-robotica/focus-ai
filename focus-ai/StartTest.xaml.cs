using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public partial class StartTest : Window
    {
        private readonly Dashboard _dashboard;
        private readonly bool _isDark;
        private const string RegPath = @"Software\FocusAI";
        private bool _done1, _done2, _done3, _done4;

        private readonly DispatcherTimer _timer = new();
        private TimeSpan _elapsed = TimeSpan.Zero;
        private bool _timerStarted = false;

        private static readonly Color CardDoneBg = Color.FromRgb(14, 30, 14);
        private static readonly Color CardDoneBorder = Color.FromRgb(34, 197, 94);

        private static readonly string EyeTrackerDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         @"..\..\..\..\EyeTracker-main\Webcam3DTracker"));
        private const string PythonScript = "MonitorTracking.py";
        private const string ArduinoPort = "COM4";

        private readonly string _dbUrl =
            ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private readonly string _sendGridApiKey =
            ConfigurationManager.AppSettings["SendGridApiKey"] ?? "";
        private readonly string _sendGridEmail =
            ConfigurationManager.AppSettings["SendGridEmail"] ?? "";

        private string _mapData = "";
        private double _reactionTimeSec = 0;
        private double _goNoGoAccuracy = 0;
        private CptHybridResult? _cptResult;

        private static readonly HttpClient _http = new();

        public StartTest(Dashboard dashboard, bool isDark)
        {
            InitializeComponent();
            LanguageManager.Register(this);
            WindowHelper.MoveToPrimaryMonitor(this);
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
            TimerStatusText.Text = LanguageManager.T("Running");
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

            BioCollector.Instance.StartStreaming(reset: true);
        }

        private void StopTimer()
        {
            _timer.Stop();
            TimerStatusText.Text = LanguageManager.T("Completed");
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
        }

        private void StartStep1_Click(object sender, RoutedEventArgs e) => RunStep(1);
        private void StartStep2_Click(object sender, RoutedEventArgs e) => RunStep(2);
        private void StartStep3_Click(object sender, RoutedEventArgs e) => RunStep(3);
        private void StartStep4_Click(object sender, RoutedEventArgs e) => RunStep(4);

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
                    if (AllStepsDone())
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
                if (!AllStepsDone())
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

                case 4:
                    var cptWin = new CptHybridTest(_isDark);
                    bool? cptCompleted = cptWin.ShowDialog();
                    if (cptCompleted == true && cptWin.Result != null)
                    {
                        _cptResult = cptWin.Result;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private async Task<bool> RunEyeTrackerAsync()
        {
            string scriptPath = Path.Combine(EyeTrackerDir, PythonScript);

            if (!Directory.Exists(EyeTrackerDir))
            {
                MessageBox.Show($"The eye-tracker directory was not found:\n{EyeTrackerDir}",
                    "Eye Tracker Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"The Python script was not found:\n{scriptPath}",
                    "Eye Tracker Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Use the virtual environment's Python interpreter if it exists
            string venvPython = Path.Combine(EyeTrackerDir, "eyetracker_env", "Scripts", "python.exe");
            string pythonExe = File.Exists(venvPython) ? venvPython : "python";

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
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
                var moveWindowTask = WindowHelper.MoveProcessWindowsToSecondMonitorAsync(process);

                string stdoutData = await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                await moveWindowTask;

                if (!string.IsNullOrWhiteSpace(stdoutData))
                {
                    _mapData = stdoutData.Trim();
                    return true;
                }

                MessageBox.Show("The script did not return coordinates.\nCheck the eye tracker.",
                    "Focus AI – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                MessageBox.Show($"Python was not found in PATH.\n\n{ex.Message}",
                    "Python Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n{ex.Message}",
                    "Eye Tracker Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                case 4: _done4 = true; break;
            }
            RefreshUI();
        }

        private bool AllStepsDone() => _done1 && _done2 && _done3 && _done4;

        private void RefreshUI()
        {
            UpdateCard(Card1Border, Status1Badge, Status1Text, StartBtn1, StartBtn1Text, _done1);
            UpdateCard(Card2Border, Status2Badge, Status2Text, StartBtn2, StartBtn2Text, _done2);
            UpdateCard(Card3Border, Status3Badge, Status3Text, StartBtn3, StartBtn3Text, _done3);
            UpdateCard(Card4Border, Status4Badge, Status4Text, StartBtn4, StartBtn4Text, _done4);

            var green = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            var gray = new SolidColorBrush(Color.FromRgb(55, 65, 81));
            ProgDot1.Fill = _done1 ? green : gray;
            ProgDot2.Fill = _done2 ? green : gray;
            ProgDot3.Fill = _done3 ? green : gray;
            ProgDot4.Fill = _done4 ? green : gray;

            int doneCount = (_done1 ? 1 : 0) + (_done2 ? 1 : 0) + (_done3 ? 1 : 0) + (_done4 ? 1 : 0);
            ProgressText.Text = $"{doneCount} / 4 {LanguageManager.T("stages completed")}";
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
                statusText.Text = $"✓  {LanguageManager.T("Completed")}";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));

                startBtn.IsEnabled = false;
                startBtnText.Text = LanguageManager.T("Completed");
            }
            else
            {
                border.Background = (SolidColorBrush)FindResource("BgCard");
                border.BorderBrush = (SolidColorBrush)FindResource("BgSep");
                border.BorderThickness = new Thickness(1.5);

                statusBadge.Background = (SolidColorBrush)FindResource("BgNavActive");
                statusText.Text = LanguageManager.T("Not completed");
                statusText.Foreground = (SolidColorBrush)FindResource("TxtMuted");

                startBtn.IsEnabled = true;
                startBtnText.Text = LanguageManager.T("▶  Start stage");
            }
        }

        private async Task ShowCompletionMessageAsync()
        {
            string time = _elapsed.ToString(@"mm\:ss");

            string summary =
                $"🎉 {LanguageManager.T("All stages are complete!")}\n\n" +
                $"{LanguageManager.T("Total time:")} {time}\n" +
                $"{LanguageManager.T("Do you want to save the results to Firebase?")}";

            var result = MessageBox.Show(summary, LanguageManager.T("Test completed"),
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                await SaveResultsAsync();

            _dashboard.Show();
            Close();
        }

        private double ComputeScore(string mapStr, double reactionSec,
                                    double goNoGoAcc, double cptAccuracy, string distStr)
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

            double goNoGoScore = goNoGoAcc / 100.0 * 20.0;
            double cptScore = cptAccuracy / 100.0 * 10.0;

            int distZeroCount = 0;
            if (!string.IsNullOrWhiteSpace(distStr))
            {
                distZeroCount = distStr.Split(',')
                    .Count(v => v.Trim() == "0");
            }
            double penalty = 5.0 * distZeroCount;

            double score = mapScore + rtScore + goNoGoScore + cptScore - penalty;

            return Math.Round(Math.Max(0, Math.Min(100, score)), 2);
        }

        private async Task SaveResultsAsync()
        {
            var bio = BioCollector.Instance;

            string ecgStr = string.Join(";", bio.Ecg.Select(s => $"{s.EcgDreapta},{s.EcgStanga}"));
            string hrStr = string.Join(",", bio.HeartRate.Where(v => v != 0));
            string spo2Str = string.Join(",", bio.SpO2.Where(v => v != 0));
            string distStr = string.Join(",", bio.Distance.Select(d => d ? "1" : "0"));

            double cptAccuracy = _cptResult?.Accuracy ?? 0;
            double scor = ComputeScore(_mapData, _reactionTimeSec, _goNoGoAccuracy, cptAccuracy, distStr);

            string dateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string duration = _elapsed.ToString(@"mm\:ss");

            string uid = FocusSession.DataOwnerId;
            string token = GetReg("IdToken");
            string testId = await GetNextTestIdAsync(uid);
            var payload = new
            {
                dateTime,
                duration,
                map = _mapData,
                ecg = ecgStr,
                hr = hrStr,
                spo2 = spo2Str,
                dist = distStr,
                tr2 = Math.Round(_reactionTimeSec, 3),
                precizie_gonogo = Math.Round(_goNoGoAccuracy, 2),
                cpt = _cptResult == null ? null : new
                {
                    accuracy = Math.Round(_cptResult.Accuracy, 2),
                    mean_reaction_time_ms = Math.Round(_cptResult.MeanReactionTimeMs, 2),
                    hit_rate = Math.Round(_cptResult.HitRate, 4),
                    false_alarm_rate = Math.Round(_cptResult.FalseAlarmRate, 4),
                    hits = _cptResult.Hits,
                    misses = _cptResult.Misses,
                    false_alarms = _cptResult.FalseAlarms,
                    correct_rejections = _cptResult.CorrectRejections,
                    interpretation = _cptResult.Interpretation,
                    trials = _cptResult.Trials.Select(t => new
                    {
                        stimulus = t.Stimulus,
                        previous_stimulus = t.PreviousStimulus,
                        is_target = t.IsTarget,
                        responded = t.Responded,
                        reaction_time_ms = t.ReactionTimeMs,
                        response_type = t.ResponseType
                    }).ToList()
                },
                scor = Math.Round(scor, 2)
            };
            string json = JsonSerializer.Serialize(payload);

            string newUrl = $"{_dbUrl.TrimEnd('/')}/patients/{uid}/testResults/{testId}.json?auth={token}";
            string rootResultsUrl = $"{_dbUrl.TrimEnd('/')}/testResults/{uid}/{testId}.json?auth={token}";
            string legacyUrl = $"{_dbUrl.TrimEnd('/')}/{uid}/tests/{testId}.json?auth={token}";

            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PutAsync(newUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    await _http.PutAsync(rootResultsUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                    await _http.PutAsync(legacyUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                }

                if (response.IsSuccessStatusCode)
                {
                    bool emailSent = await SendEmailToDoctorAsync(uid, testId);
                    if (emailSent)
                    {
                        MessageBox.Show($"Results were saved to Firebase.\nTest ID: {testId}\nAn email was sent to the doctor.",
                            "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Results were saved to Firebase.\nTest ID: {testId}\nThe email could not be sent to the doctor (missing address or error).",
                            "Focus AI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    string body = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Firebase error ({response.StatusCode}):\n{body}",
                        "Focus AI", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not send data:\n{ex.Message}",
                    "Focus AI", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<bool> SendEmailToDoctorAsync(string uid, string testId)
        {
            try
            {
                string doctorEmailUrl = $"{_dbUrl.TrimEnd('/')}/{uid}/profile/doctor-email.json";
                var emailResponse = await _http.GetAsync(doctorEmailUrl);
                if (!emailResponse.IsSuccessStatusCode)
                    return false;

                string emailJson = await emailResponse.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(emailJson) || emailJson == "null")
                    return false;
                string doctorEmail = emailJson.Trim('"');
                if (string.IsNullOrWhiteSpace(doctorEmail))
                    return false;
                if (string.IsNullOrEmpty(_sendGridApiKey) || string.IsNullOrEmpty(_sendGridEmail))
                    return false;

                string link = $"https://sitefocus.vercel.app/{uid}/{testId}";
                string subject = "Focus AI - Patient test results";
                string htmlContent = $@"
                    <html>
                    <body>
                        <h2>Focus AI</h2>
                        <p>A patient completed the cognitive test.</p>
                        <p><strong>Test ID:</strong> {testId}</p>
                        <p><strong>Results link:</strong> <a href='{link}'>{link}</a></p>
                        <p>Please open the link to view the full details.</p>
                        <hr/>
                        <small>This message was generated automatically by Focus AI.</small>
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

        private async Task<string> GetNextTestIdAsync(string uid)
        {
            try
            {
                string token = GetReg("IdToken");
                string url = $"{_dbUrl.TrimEnd('/')}/patients/{uid}/testResults.json?shallow=true&auth={token}";
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
                "Are you sure you want to cancel the test?\nCurrent progress will not be saved.",
                "Cancel test", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
