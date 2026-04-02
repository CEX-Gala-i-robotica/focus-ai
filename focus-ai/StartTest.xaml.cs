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

            double score = mapScore + rtScore + goNoGoScore + 10;

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
                    // Construim un obiect cu toate datele testului pentru a le trimite în email
                    var testData = new
                    {
                        dateTime,
                        duration,
                        mapData = _mapData,
                        ecgStr,
                        hrStr,
                        spo2Str,
                        distStr,
                        reactionTime = _reactionTimeSec,
                        goNoGoAccuracy = _goNoGoAccuracy,
                        score = scor
                    };

                    bool emailSent = await SendEmailToDoctorAsync(uid, testId, testData);
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

        private async Task<bool> SendEmailToDoctorAsync(string uid, string testId, object testData)
        {
            try
            {
                // 1. Obține emailul medicului și profilul pacientului din Firebase
                string profileUrl = $"{_dbUrl.TrimEnd('/')}/{uid}/profile.json";
                var profileResponse = await _http.GetAsync(profileUrl);
                if (!profileResponse.IsSuccessStatusCode)
                    return false;

                string profileJson = await profileResponse.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(profileJson) || profileJson == "null")
                    return false;

                // Parsează manual JSON-ul profilului (simplu, fără biblioteci externe)
                var profile = ParseProfileJson(profileJson);
                if (profile == null || string.IsNullOrEmpty(profile.DoctorEmail))
                    return false;

                // 2. Pregătește HTML-ul e-mailului
                string htmlContent = BuildTestResultsHtml(profile, testId, testData);

                // 3. Trimite prin SendGrid
                if (string.IsNullOrEmpty(_sendGridApiKey) || string.IsNullOrEmpty(_sendGridEmail))
                    return false;

                var emailPayload = new
                {
                    personalizations = new[]
                    {
                        new
                        {
                            to = new[] { new { email = profile.DoctorEmail } },
                            subject = $"Focus AI - Rezultate test pentru {profile.Name} {profile.Surname}"
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

        private string BuildTestResultsHtml(dynamic profile, string testId, dynamic testData)
        {
            // Extrage datele testului
            string dateTime = testData.dateTime;
            string duration = testData.duration;
            string mapData = testData.mapData ?? "";
            string ecgStr = testData.ecgStr ?? "";
            string hrStr = testData.hrStr ?? "";
            string spo2Str = testData.spo2Str ?? "";
            string distStr = testData.distStr ?? "";
            double reactionTime = testData.reactionTime;
            double goNoGoAccuracy = testData.goNoGoAccuracy;
            double score = testData.score;

            // Calculează statistici
            var mapPoints = ParseMapPoints(mapData);
            var ecgPairs = ParseEcgPairs(ecgStr);
            var hrValues = ParseNumberList(hrStr);
            var spo2Values = ParseNumberList(spo2Str);
            var distValues = ParseNumberList(distStr);

            int mapCount = mapPoints.Count;
            int ecgCount = ecgPairs.Count;
            double hrMin = hrValues.Count > 0 ? hrValues.Min() : 0;
            double hrMax = hrValues.Count > 0 ? hrValues.Max() : 0;
            double spo2Min = spo2Values.Count > 0 ? spo2Values.Min() : 0;
            double spo2Max = spo2Values.Count > 0 ? spo2Values.Max() : 0;
            int distActive = distValues.Count(v => v > 0);
            int distTotal = distValues.Count;

            // Mostre date (primele 10 puncte MAP, primele 10 perechi ECG)
            string mapSample = string.Join(", ", mapPoints.Take(10).Select(p => $"({p.X},{p.Y})"));
            string ecgSample = string.Join(", ", ecgPairs.Take(10).Select(p => $"{p.Ch1}/{p.Ch2}"));

            // Construiește HTML
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        background-color: #111827;
                        color: #F9FAFB;
                        margin: 0;
                        padding: 20px;
                    }}
                    .container {{
                        max-width: 800px;
                        margin: 0 auto;
                        background-color: #1F2937;
                        border-radius: 12px;
                        padding: 24px;
                        box-shadow: 0 4px 6px rgba(0,0,0,0.1);
                    }}
                    h1, h2 {{
                        border-bottom: 1px solid #374151;
                        padding-bottom: 8px;
                        color: #F9FAFB;
                    }}
                    h1 {{ font-size: 28px; margin-top: 0; }}
                    h2 {{ font-size: 22px; margin-top: 24px; }}
                    .grid {{
                        display: grid;
                        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
                        gap: 16px;
                        margin-bottom: 20px;
                    }}
                    .card {{
                        background-color: #374151;
                        border-radius: 8px;
                        padding: 12px 16px;
                    }}
                    .card strong {{
                        color: #9CA3AF;
                        font-weight: 600;
                    }}
                    .stats-bar {{
                        display: flex;
                        flex-wrap: wrap;
                        justify-content: space-between;
                        background-color: #374151;
                        border-radius: 8px;
                        padding: 16px;
                        margin: 20px 0;
                        text-align: center;
                    }}
                    .stat {{
                        flex: 1;
                        min-width: 100px;
                    }}
                    .stat-value {{
                        font-size: 24px;
                        font-weight: bold;
                        color: #4DFFDF;
                    }}
                    .stat-label {{
                        font-size: 12px;
                        color: #9CA3AF;
                    }}
                    table {{
                        width: 100%;
                        border-collapse: collapse;
                        margin: 16px 0;
                    }}
                    th, td {{
                        border: 1px solid #4B5563;
                        padding: 8px 12px;
                        text-align: left;
                        vertical-align: top;
                    }}
                    th {{
                        background-color: #374151;
                        color: #F9FAFB;
                    }}
                    .sample {{
                        background-color: #111827;
                        padding: 8px;
                        border-radius: 6px;
                        font-family: monospace;
                        font-size: 13px;
                        overflow-x: auto;
                    }}
                    .footer {{
                        margin-top: 32px;
                        font-size: 12px;
                        text-align: center;
                        color: #6B7280;
                        border-top: 1px solid #374151;
                        padding-top: 16px;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h1>📊 Focus AI – Rezultate Test Cognitiv</h1>
                    
                    <h2>👤 Profil pacient</h2>
                    <div class='grid'>
                        <div class='card'><strong>Nume:</strong> {profile.Name} {profile.Surname}</div>
                        <div class='card'><strong>Data nașterii:</strong> {profile.BirthDate}</div>
                        <div class='card'><strong>Telefon:</strong> {profile.PhoneNumber}</div>
                        <div class='card'><strong>Email medic:</strong> {profile.DoctorEmail}</div>
                        <div class='card'><strong>Telefon medic:</strong> {profile.DoctorPhone}</div>
                        <div class='card'><strong>ID test:</strong> {testId}</div>
                        <div class='card'><strong>Data & ora:</strong> {dateTime}</div>
                        <div class='card'><strong>Durată:</strong> {duration}</div>
                        <div class='card'><strong>Scor total:</strong> {score:F2}</div>
                        <div class='card'><strong>Acuratețe Go/No-Go:</strong> {goNoGoAccuracy:F2}%</div>
                        <div class='card'><strong>Timp reacție (tr²):</strong> {reactionTime:F3} s</div>
                    </div>

                    <div class='stats-bar'>
                        <div class='stat'><div class='stat-value'>{mapCount}</div><div class='stat-label'>Puncte MAP</div></div>
                        <div class='stat'><div class='stat-value'>{ecgCount}</div><div class='stat-label'>Mostre ECG</div></div>
                        <div class='stat'><div class='stat-value'>{spo2Min:F0}–{spo2Max:F0}%</div><div class='stat-label'>SpO₂</div></div>
                        <div class='stat'><div class='stat-value'>{hrMin:F0}–{hrMax:F0}</div><div class='stat-label'>HR (bpm)</div></div>
                        <div class='stat'><div class='stat-value'>{distActive}/{distTotal}</div><div class='stat-label'>DIST activ</div></div>
                    </div>

                    <h2>📍 Harta atenției (MAP)</h2>
                    <p><strong>Total puncte:</strong> {mapCount}</p>
                    <div class='sample'>Mostră puncte (x,y): {mapSample}</div>

                    <h2>❤️ Electrocardiogramă (ECG)</h2>
                    <p><strong>Total perechi:</strong> {ecgCount}</p>
                    <div class='sample'>Mostră valori (CH1, CH2): {ecgSample}</div>

                    <h2>📈 Frecvență cardiacă (HR)</h2>
                    <p>Minim: {hrMin:F0} bpm, Maxim: {hrMax:F0} bpm</p>

                    <h2>🫁 Saturație oxigen (SpO₂)</h2>
                    <p>Minim: {spo2Min:F0}%, Maxim: {spo2Max:F0}%</p>

                    <h2>📏 Distanță (DIST) – momente active</h2>
                    <p>{distActive} din {distTotal} înregistrări au fost active (distanță > 0).</p>

                    <div class='footer'>
                        Acest raport a fost generat automat de aplicația Focus AI.<br>
                        Pentru detalii complete, accesați platforma web Focus AI.
                    </div>
                </div>
            </body>
            </html>";
        }

        // Helper pentru parsare JSON simplă a profilului
        private dynamic ParseProfileJson(string json)
        {
            try
            {
                // Elimină ghilimelele de la început/sfârșit dacă există
                json = json.Trim();
                if (json.StartsWith("\"")) json = json.Substring(1, json.Length - 2);

                var dict = new Dictionary<string, string>();
                // Parsare manuală foarte simplă: caută perechi "key":"value"
                var parts = json.Split(',');
                foreach (var part in parts)
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().Trim('"');
                        string val = kv[1].Trim().Trim('"');
                        dict[key] = val;
                    }
                }

                return new
                {
                    Name = dict.ContainsKey("name") ? dict["name"] : "",
                    Surname = dict.ContainsKey("surname") ? dict["surname"] : "",
                    BirthDate = dict.ContainsKey("birth-date") ? dict["birth-date"] : "",
                    PhoneNumber = dict.ContainsKey("phone-number") ? dict["phone-number"] : "",
                    DoctorEmail = dict.ContainsKey("doctor-email") ? dict["doctor-email"] : "",
                    DoctorPhone = dict.ContainsKey("doctor-phone") ? dict["doctor-phone"] : ""
                };
            }
            catch
            {
                return null;
            }
        }

        private List<(double X, double Y)> ParseMapPoints(string mapData)
        {
            var result = new List<(double, double)>();
            if (string.IsNullOrWhiteSpace(mapData)) return result;
            var points = mapData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pt in points)
            {
                var parts = pt.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double y))
                {
                    result.Add((x, y));
                }
            }
            return result;
        }

        private List<(double Ch1, double Ch2)> ParseEcgPairs(string ecgStr)
        {
            var result = new List<(double, double)>();
            if (string.IsNullOrWhiteSpace(ecgStr)) return result;
            var pairs = ecgStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var vals = pair.Split(',');
                if (vals.Length == 2 &&
                    double.TryParse(vals[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double ch1) &&
                    double.TryParse(vals[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double ch2))
                {
                    result.Add((ch1, ch2));
                }
            }
            return result;
        }

        private List<double> ParseNumberList(string str)
        {
            var result = new List<double>();
            if (string.IsNullOrWhiteSpace(str)) return result;
            var items = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                if (double.TryParse(item.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    result.Add(val);
                }
            }
            return result;
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