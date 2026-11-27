using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        private static string EyeTrackerDir => ResolveEyeTrackerDirectory();
        private const string PythonScript = "MonitorTracking.py";

        private readonly string _dbUrl = AppSettings.Get("RealtimeDatabaseUrl");
        private readonly string _emailServiceUrl = AppSettings.Get("EmailServiceUrl");

        private string _mapData = "";
        private double _reactionTimeSec = 0;
        private double _goNoGoAccuracy = 0;
        private CptHybridResult? _cptResult;
        private GazeSessionSummary _gazeSession = GazeSessionSummary.Empty;

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

            BioCollector.Instance.ConnectionStateChanged += BioCollector_ConnectionStateChanged;
            Loaded += StartTest_Loaded;
            Closing += StartTest_Closing;
        }

        private async void StartTest_Loaded(object sender, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Arduino: searching serial ports...";
            bool connected = await BioCollector.Instance.TryOpenAsync();
            if (!connected && !BioCollector.Instance.IsConnected)
                DeviceStatusText.Text = "Arduino: not detected — automatic reconnect active";
        }

        private void BioCollector_ConnectionStateChanged(string portName, bool connected)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => BioCollector_ConnectionStateChanged(portName, connected)));
                return;
            }

            DeviceStatusText.Text = connected
                ? $"Arduino: connected on {portName}"
                : "Arduino: disconnected — reconnecting automatically";
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
            SetInteractionLocked(true);

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
                        await ShowCompletionMessageAsync();
                        return;
                    }
                }
            }
            finally
            {
                if (!AllStepsDone())
                    SetInteractionLocked(false);
            }
        }

        private async Task<bool> LaunchStepWindowAsync(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1:
                    return await RunEyeTrackerAsync();

                case 2:
                    var buzzerWin = new BuzzerTest(_isDark) { Owner = this };
                    WindowHelper.MoveToSecondMonitor(buzzerWin);
                    buzzerWin.ShowDialog();
                    if (buzzerWin.ReactionTime.HasValue)
                        _reactionTimeSec = buzzerWin.ReactionTime.Value;
                    return true;

                case 3:
                    var goNoGoWin = new GoNoGoTest(_isDark) { Owner = this };
                    WindowHelper.MoveToSecondMonitor(goNoGoWin);
                    goNoGoWin.ShowDialog();
                    _goNoGoAccuracy = goNoGoWin.Accuracy;
                    return true;

                case 4:
                    var cptWin = new CptHybridTest(_isDark) { Owner = this };
                    WindowHelper.MoveToSecondMonitor(cptWin);
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
            _mapData = "";
            _gazeSession = GazeSessionSummary.Empty;
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

            string pythonExe = ResolveEyeTrackerPythonExecutable();
            string gazeSamplesPath = Path.Combine(EyeTrackerDir, "gaze_samples.csv");
            string legacyCoordsPath = Path.Combine(EyeTrackerDir, "gaze_coords.txt");
            try
            {
                if (File.Exists(gazeSamplesPath)) File.Delete(gazeSamplesPath);
                if (File.Exists(legacyCoordsPath)) File.Delete(legacyCoordsPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot prepare eye-tracker output files:\n{ex.Message}",
                    "Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

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
            psi.Environment["FOCUS_AI_LANG"] = LanguageManager.Current == AppLanguage.Romanian ? "ro" : "en";
            psi.Environment["FOCUS_AI_MONITOR"] = "secondary";

            try
            {
                DateTime scriptStartTime = DateTime.Now;
                using var process = new Process { StartInfo = psi };
                process.Start();
                var moveWindowTask = WindowHelper.MoveProcessWindowsToSecondMonitorAsync(process);

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                string stdoutData = await stdoutTask;
                string stderrData = await stderrTask;
                await moveWindowTask;

                string coordsPayload = ExtractCoordinatePayload(stdoutData);
                if (string.IsNullOrWhiteSpace(coordsPayload))
                    coordsPayload = TryReadCoordinatePayloadFromFile(legacyCoordsPath, scriptStartTime);

                _mapData = coordsPayload;
                _gazeSession = GazeSessionSummary.ReadCsv(gazeSamplesPath);

                if (_gazeSession.TotalSamples > 0)
                {
                    if (_gazeSession.ValidSamples == 0)
                    {
                        MessageBox.Show(
                            "The eye-tracker session completed, but no valid gaze intersections were recorded. " +
                            "The session will be saved with gaze marked as unavailable; no attention values will be fabricated.",
                            "Focus AI – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return true;
                }

                string details = BuildEyeTrackerErrorDetails(process.ExitCode, stdoutData, stderrData);
                MessageBox.Show(LanguageManager.T("The script did not return timestamped gaze samples.\nCheck the eye tracker.") + details,
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

        private void SetInteractionLocked(bool locked)
        {
            StartBtn1.IsEnabled = !locked && !_done1;
            StartBtn2.IsEnabled = !locked && !_done2;
            StartBtn3.IsEnabled = !locked && !_done3;
            StartBtn4.IsEnabled = !locked && !_done4;
            TimerStatusText.Text = locked
                ? LanguageManager.T("Running")
                : (_timerStarted ? LanguageManager.T("Running") : LanguageManager.T("Waiting"));
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

        private async Task SaveResultsAsync()
        {
            var bio = BioCollector.Instance;

            string ecgStr = string.Join(";", bio.Ecg.Select(s => $"{s.EcgDreapta},{s.EcgStanga}"));
            string hrStr = string.Join(",", bio.HeartRate.Where(v => v != 0));
            string spo2Str = string.Join(",", bio.SpO2.Where(v => v != 0));
            string distStr = string.Join(",", bio.Distance.Select(d => d ? "1" : "0"));
            string distCmStr = string.Join(",", bio.DistanceCm.Select(v => v.ToString(CultureInfo.InvariantCulture)));
            string rrStr = string.Join(",", bio.RrIntervalsMs.Select(value => value.ToString("F3", CultureInfo.InvariantCulture)));

            HrvMetricsResult hrv = HrvMetrics.Analyze(bio.RrIntervalsMs);
            AttentionScoreResult attention = TestScoreCalculator.EstimateAttention(new AttentionEstimationInput
            {
                MapData = _mapData,
                BuzzerReactionTimeSeconds = _reactionTimeSec > 0 ? _reactionTimeSec : null,
                GoNoGoAccuracyPercent = _done3 ? _goNoGoAccuracy : null,
                CptAccuracyPercent = _cptResult?.Accuracy,
                CptHitRate = _cptResult?.HitRate,
                CptFalseAlarmRate = _cptResult?.FalseAlarmRate,
                CptTauMs = _cptResult?.ReactionTimeDistribution.IsAvailable == true
                    ? _cptResult.ReactionTimeDistribution.TauMs
                    : null,
                CptReactionTimeCv = _cptResult?.ReactionTimeCoefficientOfVariation,
                CptReactionTimeSampleCount = _cptResult?.ReactionTimeDistribution.SampleCount ?? 0,
                Hrv = hrv,
                GazeOnTargetFraction = _gazeSession.OnTargetFraction,
                GazeTargetSampleCount = _gazeSession.TrackedTargetSamples,
                GazeTargetTrackingCoverage = _gazeSession.TargetTrackingCoverage,
                GazeValidFraction = _gazeSession.ValidFraction
            });
            double scor = attention.Score;

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
                // Keep the historical Firebase key for older app versions, but
                // mark the new measurements as raw analogue sensor values.
                ecg = ecgStr,
                analog_signal = ecgStr,
                analog_signal_source = "MyoWare analog output A0 (raw ADC; not a clinically validated ECG waveform)",
                analog_signal_is_clinically_validated_ecg = false,
                hr = hrStr,
                spo2 = spo2Str,
                dist = distStr,
                dist_cm = distCmStr,
                analog_samples = bio.Ecg.Select(sample => new
                {
                    device_timestamp_ms = sample.DeviceTimestampMs,
                    channel_a0_raw = sample.EcgDreapta,
                    channel_a1_raw = sample.EcgStanga >= 0 ? sample.EcgStanga : (int?)null
                }).ToList(),
                rr_ms = rrStr,
                rr_source = "MAX30102_PPG",
                rr_measurement_type = "PRV",
                ppg_rr_samples = bio.PpgRrSamples.Select(sample => new
                {
                    device_timestamp_ms = sample.DeviceTimestampMs,
                    interval_ms = sample.IntervalMs,
                    ir_amplitude = sample.IrAmplitude
                }).ToList(),
                bio_device = new
                {
                    connected_port = bio.ConnectedPortName,
                    last_detected_port = bio.LastDetectedPortName,
                    ppg_sensor_ready = bio.PpgSensorReady,
                    finger_detected = bio.FingerDetected,
                    analog_channel_source = "MyoWare raw analog output A0",
                    analog_channel_is_clinically_validated_ecg = false,
                    second_analog_channel_available = bio.Ecg.Any(sample => sample.EcgStanga >= 0),
                    distance_sensor_available = bio.DistanceCm.Count > 0,
                    valid_distance_sample_count = bio.DistanceCm.Count
                },
                gaze = new
                {
                    total_samples = _gazeSession.TotalSamples,
                    valid_samples = _gazeSession.ValidSamples,
                    valid_fraction = _gazeSession.ValidFraction,
                    phase2_target_samples = _gazeSession.TargetSamples,
                    phase2_tracked_target_samples = _gazeSession.TrackedTargetSamples,
                    on_target_samples = _gazeSession.OnTargetSamples,
                    on_target_fraction = _gazeSession.OnTargetFraction,
                    target_tracking_coverage = _gazeSession.TargetTrackingCoverage,
                    pupil_measurement_available = false,
                    pupil_measurement_note = "The current MediaPipe face-mesh pipeline does not measure pupil diameter."
                },
                hrv = new
                {
                    rr_sample_count = hrv.RrSampleCount,
                    has_time_domain_metrics = hrv.HasTimeDomainMetrics,
                    sdnn_ms = hrv.SdnnMs,
                    rmssd_ms = hrv.RmssdMs,
                    has_dfa_alpha1 = hrv.HasDfaAlpha1,
                    dfa_alpha1 = hrv.DfaAlpha1,
                    has_sample_entropy = hrv.HasSampleEntropy,
                    sample_entropy = hrv.SampleEntropy
                },
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
                    reaction_time_standard_deviation_ms = _cptResult.ReactionTimeStandardDeviationMs,
                    reaction_time_cv = _cptResult.ReactionTimeCoefficientOfVariation,
                    reaction_time_sample_count = _cptResult.ReactionTimeDistribution.SampleCount,
                    ex_gaussian_available = _cptResult.ReactionTimeDistribution.IsAvailable,
                    ex_gaussian_reliable = _cptResult.ReactionTimeDistribution.IsReliable,
                    ex_gaussian_converged = _cptResult.ReactionTimeDistribution.Converged,
                    ex_gaussian_log_likelihood = _cptResult.ReactionTimeDistribution.IsAvailable
                        ? _cptResult.ReactionTimeDistribution.LogLikelihood
                        : (double?)null,
                    mu_ms = _cptResult.ReactionTimeDistribution.IsAvailable
                        ? _cptResult.ReactionTimeDistribution.MuMs
                        : (double?)null,
                    sigma_ms = _cptResult.ReactionTimeDistribution.IsAvailable
                        ? _cptResult.ReactionTimeDistribution.SigmaMs
                        : (double?)null,
                    tau_ms = _cptResult.ReactionTimeDistribution.IsAvailable
                        ? _cptResult.ReactionTimeDistribution.TauMs
                        : (double?)null,
                    tau_subscore = attention.TauSubscore,
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
                attention = new
                {
                    model = "scalar-kalman-v1",
                    estimate = Math.Round(attention.Posterior.Estimate, 4),
                    score = Math.Round(attention.Score, 2),
                    posterior_variance = Math.Round(attention.Posterior.PosteriorVariance, 6),
                    posterior_standard_deviation = Math.Round(attention.Posterior.PosteriorStandardDeviation, 4),
                    confidence_low = Math.Round(attention.Posterior.ConfidenceLow, 4),
                    confidence_high = Math.Round(attention.Posterior.ConfidenceHigh, 4),
                    observation_count = attention.Posterior.ObservationCount,
                    behavioral_subscore = attention.BehavioralSubscore,
                    gaze_on_screen_fraction = attention.GazeOnScreenFraction,
                    gaze_sample_count = attention.GazeSampleCount,
                    gaze_on_target_fraction = attention.GazeOnTargetFraction,
                    gaze_target_sample_count = attention.GazeTargetSampleCount,
                    gaze_valid_fraction = attention.GazeValidFraction,
                    prv_descriptive_composite = attention.HrvAdaptivitySubscore,
                    pupil_engagement_subscore = attention.PupilEngagementSubscore,
                    tau_subscore = attention.TauSubscore,
                    updates = attention.Posterior.Updates.Select(update => new
                    {
                        source = update.Source,
                        observation = Math.Round(update.Observation, 4),
                        observation_variance = Math.Round(update.ObservationVariance, 6),
                        kalman_gain = Math.Round(update.KalmanGain, 4),
                        posterior_estimate = Math.Round(update.PosteriorEstimate, 4),
                        posterior_standard_deviation = Math.Round(update.PosteriorStandardDeviation, 4),
                        detail = update.Detail
                    }).ToList()
                },
                scor = Math.Round(scor, 2),
                scoreVersion = TestScoreCalculator.CurrentVersion
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
                        MessageBox.Show($"Results were saved to Firebase.\nTest ID: {testId}\nThe email notification was not sent. Configure the authenticated email backend to enable notifications.",
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
            if (!Uri.TryCreate(_emailServiceUrl, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                return false;

            string token = GetReg("IdToken");
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(testId))
                return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { patientId = uid, testId }),
                    Encoding.UTF8,
                    "application/json");

                using HttpResponseMessage response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
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
                string value = k?.GetValue(key)?.ToString() ?? "";
                return key == "IdToken" ? SessionTokenProtector.Unprotect(value) : value;
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

        private static string ExtractCoordinatePayload(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout))
                return "";

            var lines = stdout.Split(
                new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (LooksLikeCoordinatePayload(lines[i]))
                    return lines[i];
            }

            return "";
        }

        private static bool LooksLikeCoordinatePayload(string value)
        {
            var pairs = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pairs.Length == 0)
                return false;

            foreach (var pair in pairs)
            {
                var parts = pair.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                    return false;

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                    return false;

                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                    return false;
            }

            return true;
        }

        private static string ResolveEyeTrackerDirectory()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "EyeTracker-main", "Webcam3DTracker"),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\..\EyeTracker-main\Webcam3DTracker")),
                Path.Combine(Environment.CurrentDirectory, "EyeTracker-main", "Webcam3DTracker"),
                Path.Combine(Environment.CurrentDirectory, "..", "EyeTracker-main", "Webcam3DTracker")
            };

            return candidates
                .Select(Path.GetFullPath)
                .FirstOrDefault(path => Directory.Exists(path) &&
                    File.Exists(Path.Combine(path, PythonScript)))
                ?? candidates[1];
        }

        private static string ResolveEyeTrackerPythonExecutable()
        {
            string venv312Python = Path.Combine(EyeTrackerDir, "eyetracker_env312", "Scripts", "python.exe");
            if (IsPythonUsable(venv312Python))
                return venv312Python;

            string venvPython = Path.Combine(EyeTrackerDir, "eyetracker_env", "Scripts", "python.exe");
            if (IsPythonUsable(venvPython))
                return venvPython;

            return "python";
        }

        private static bool IsPythonUsable(string pythonExe)
        {
            if (string.IsNullOrWhiteSpace(pythonExe) || !File.Exists(pythonExe))
                return false;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = "-c \"import sys\"",
                    WorkingDirectory = EyeTrackerDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                    return false;

                process.WaitForExit(3000);
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string TryReadCoordinatePayloadFromFile(string path, DateTime scriptStartTime)
        {
            try
            {
                if (!File.Exists(path))
                    return "";

                if (File.GetLastWriteTime(path) < scriptStartTime)
                    return "";

                var matches = Regex.Matches(
                    File.ReadAllText(path),
                    @"\(\s*\d+\s*,\s*([-+]?\d+(?:\.\d+)?)\s*,\s*([-+]?\d+(?:\.\d+)?)\s*\)");

                var points = matches
                    .Cast<Match>()
                    .Select(match =>
                    {
                        double x = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 100.0;
                        double y = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 100.0;
                        return $"{x:F3},{y:F3}";
                    })
                    .ToArray();

                return points.Length > 0 ? string.Join(";", points) : "";
            }
            catch
            {
                return "";
            }
        }

        private static string BuildEyeTrackerErrorDetails(int exitCode, string stdout, string stderr)
        {
            var details = new List<string>();

            if (exitCode != 0)
                details.Add($"Exit code: {exitCode}");

            string lastError = LastNonEmptyLine(stderr);
            if (!string.IsNullOrWhiteSpace(lastError))
                details.Add($"Python: {lastError}");

            string lastOutput = LastNonEmptyLine(stdout);
            if (!string.IsNullOrWhiteSpace(lastOutput))
                details.Add($"Output: {lastOutput}");

            return details.Count == 0 ? "" : "\n\n" + string.Join("\n", details);
        }

        private static string LastNonEmptyLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return text.Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault() ?? "";
        }

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

        private void StartTest_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            BioCollector.Instance.ConnectionStateChanged -= BioCollector_ConnectionStateChanged;
            BioCollector.Instance.Close();
            if (!_dashboard.IsVisible)
                _dashboard.Show();
        }
    }
}
