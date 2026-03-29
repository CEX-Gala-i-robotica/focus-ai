using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace focus_ai
{
    public partial class StartTest : Window
    {
        private readonly Dashboard _dashboard;
        private readonly bool _isDark;

        private bool _done1, _done2, _done3;

        private readonly DispatcherTimer _timer = new();
        private TimeSpan _elapsed = TimeSpan.Zero;
        private bool _timerStarted = false;

        // Culori card finalizat – rămân independente de temă
        private static readonly Color CardDoneBg      = Color.FromRgb(14,  30,  14);
        private static readonly Color CardDoneBorder  = Color.FromRgb(34, 197, 94);

        // ─── Calea relativă către scriptul Python ───────────────────────────────
        // StartTest.cs se află în:  <solution>\focus_ai\
        // MonitorTracking.py se află în:  <solution>\EyeTracker-main\Webcam3DTracker\
        // Deci relativ față de directorul EXE-ului (bin\Debug\net8.0-windows\) ajungem
        // cu 3 niveluri în sus la rădăcina soluției, apoi intrăm în sub-folder.
        private static readonly string EyeTrackerDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         @"..\..\..\..\EyeTracker-main\Webcam3DTracker"));

        private const string PythonScript = "MonitorTracking.py";

        public StartTest(Dashboard dashboard, bool isDark)
        {
            InitializeComponent();
            _dashboard = dashboard;
            _isDark    = isDark;

            ThemeManager.Apply(_isDark);
            RefreshUI();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick    += Timer_Tick;

            this.Closing += StartTest_Closing;
        }

        // ════════════════════════════════════════════
        //  TIMER
        // ════════════════════════════════════════════

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
        }

        private void StopTimer()
        {
            _timer.Stop();
            TimerStatusText.Text = "Finalizat";
            TimerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
        }

        // ════════════════════════════════════════════
        //  CLICK ETAPE
        // ════════════════════════════════════════════

        private void StartStep1_Click(object sender, RoutedEventArgs e) => RunStep(1);
        private void StartStep2_Click(object sender, RoutedEventArgs e) => RunStep(2);
        private void StartStep3_Click(object sender, RoutedEventArgs e) => RunStep(3);

        private void RunStep(int stepIndex)
        {
            EnsureTimerStarted();
            this.Hide();

            try
            {
                bool stepCompleted = LaunchStepWindow(stepIndex);
                if (stepCompleted)
                {
                    MarkStepDone(stepIndex);
                    if (_done1 && _done2 && _done3)
                    {
                        StopTimer();
                        this.Show();
                        ShowCompletionMessage();
                        return;
                    }
                }
            }
            finally
            {
                if (!(_done1 && _done2 && _done3))
                    this.Show();
            }
        }

        // ════════════════════════════════════════════
        //  LANSARE FERESTRE / SUBPROCESE
        // ════════════════════════════════════════════

        private bool LaunchStepWindow(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1:
                    return RunEyeTracker();

                case 2:
                    MessageBox.Show("Etapa 2 – Reacție la vibrație\n\n(Placeholder – implementează fereastra testului)",
                        "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;

                case 3:
                    MessageBox.Show("Etapa 3 – Reacție vizuală GO/NO-GO\n\n(Placeholder – implementează fereastra testului)",
                        "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Rulează MonitorTracking.py ca subprocess, citește stdout-ul (coordonatele gaze)
        /// și le afișează într-un MessageBox. Returnează true dacă scriptul s-a încheiat
        /// fără erori fatale.
        /// </summary>
        private bool RunEyeTracker()
        {
            // ── 1. Verifică că folderul și scriptul există ────────────────────
            string scriptPath = Path.Combine(EyeTrackerDir, PythonScript);

            if (!Directory.Exists(EyeTrackerDir))
            {
                MessageBox.Show(
                    $"Directorul eye-tracker nu a fost găsit:\n{EyeTrackerDir}\n\n" +
                    "Verifică calea EyeTrackerDir din StartTest.cs.",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(
                    $"Scriptul Python nu a fost găsit:\n{scriptPath}",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // ── 2. Configurează procesul ──────────────────────────────────────
            var psi = new ProcessStartInfo
            {
                FileName               = "py",   // sau "python3" pe unele sisteme
                Arguments              = $"\"{PythonScript}\"",
                WorkingDirectory       = EyeTrackerDir,

                // Capturăm stdout (coordonatele) și stderr (loguri/erori)
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,       // fără fereastră consolă separată
            };

            string stdoutData = string.Empty;
            string stderrData = string.Empty;

            try
            {
                using var process = new Process { StartInfo = psi };

                process.Start();

                // Citim stdout și stderr sincron după ce procesul se termină
                // (MonitorTracking.py scrie o singură linie pe stdout când finalizează)
                stdoutData = process.StandardOutput.ReadToEnd();
                stderrData = process.StandardError.ReadToEnd();

                process.WaitForExit();

                int exitCode = process.ExitCode;

                // ── 3. Afișează rezultatele ───────────────────────────────────
                if (!string.IsNullOrWhiteSpace(stdoutData))
                {
                    // stdoutData = "mx,my;mx,my;..." — exact formatul din MonitorTracking.py
                    string[] points   = stdoutData.Trim().Split(';');
                    int       count   = points.Length;

                    // Construiește un rezumat vizual (primele 10 puncte + total)
                    int previewCount = Math.Min(count, 10);
                    var preview = new System.Text.StringBuilder();
                    for (int i = 0; i < previewCount; i++)
                        preview.AppendLine($"  [{i + 1}]  {points[i]}");
                    if (count > previewCount)
                        preview.AppendLine($"  ... și încă {count - previewCount} puncte");

                    string msg =
                        $"✅ Etapa 1 – Urmărirea privirii finalizată!\n\n" +
                        $"Total puncte gaze captate: {count}\n\n" +
                        $"Primele {previewCount} coordonate (mx, my):\n" +
                        preview.ToString() +
                        (exitCode != 0 ? $"\n⚠️ Cod ieșire Python: {exitCode}" : string.Empty);

                    MessageBox.Show(msg, "Focus AI – Rezultate Eye Tracker",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    // Niciun output pe stdout — posibil eroare Python
                    string errorInfo = string.IsNullOrWhiteSpace(stderrData)
                        ? "(niciun mesaj de eroare)"
                        : stderrData.Trim();

                    MessageBox.Show(
                        $"⚠️ Scriptul MonitorTracking.py nu a returnat coordonate.\n\n" +
                        $"Cod ieșire: {exitCode}\n\nStderr:\n{errorInfo}",
                        "Focus AI – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 2 /* ERROR_FILE_NOT_FOUND */)
            {
                // Python nu este în PATH
                MessageBox.Show(
                    "Nu s-a putut lansa Python.\n\n" +
                    "Asigură-te că 'python' (sau 'python3') este instalat și adăugat în PATH.\n\n" +
                    $"Detalii: {ex.Message}",
                    "Eroare – Python lipsă", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Eroare neașteptată la lansarea eye tracker-ului:\n\n{ex.Message}",
                    "Eroare – Eye Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ════════════════════════════════════════════
        //  MARCARE ETAPĂ FINALIZATĂ
        // ════════════════════════════════════════════

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

        // ════════════════════════════════════════════
        //  REFRESH UI
        // ════════════════════════════════════════════

        private void RefreshUI()
        {
            UpdateCard(Card1Border, Status1Badge, Status1Text, StartBtn1, StartBtn1Text, Card1Title, _done1);
            UpdateCard(Card2Border, Status2Badge, Status2Text, StartBtn2, StartBtn2Text, Card2Title, _done2);
            UpdateCard(Card3Border, Status3Badge, Status3Text, StartBtn3, StartBtn3Text, Card3Title, _done3);

            ProgDot1.Fill = _done1 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(55, 65, 81));
            ProgDot2.Fill = _done2 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(55, 65, 81));
            ProgDot3.Fill = _done3 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(55, 65, 81));

            int doneCount = (_done1 ? 1 : 0) + (_done2 ? 1 : 0) + (_done3 ? 1 : 0);
            ProgressText.Text = $"{doneCount} / 3 etape finalizate";
        }

        private void UpdateCard(
            Border border, Border statusBadge, TextBlock statusText,
            Button startBtn, TextBlock startBtnText, TextBlock titleText,
            bool done)
        {
            if (done)
            {
                border.Background  = new SolidColorBrush(CardDoneBg);
                border.BorderBrush = new SolidColorBrush(CardDoneBorder);
                border.BorderThickness = new Thickness(1.5);

                statusBadge.Background = new SolidColorBrush(Color.FromRgb(20, 83, 45));
                statusText.Text        = "✓  Finalizată";
                statusText.Foreground  = new SolidColorBrush(Color.FromRgb(74, 222, 128));

                startBtn.IsEnabled  = false;
                startBtnText.Text   = "Finalizată";
            }
            else
            {
                border.Background      = (SolidColorBrush)FindResource("BgCard");
                border.BorderBrush     = (SolidColorBrush)FindResource("BgSep");
                border.BorderThickness = new Thickness(1.5);

                statusBadge.Background = (SolidColorBrush)FindResource("BgNavActive");
                statusText.Text        = "Neparcursă";
                statusText.Foreground  = (SolidColorBrush)FindResource("TxtMuted");

                startBtn.IsEnabled  = true;
                startBtnText.Text   = "▶  Start etapă";
            }
        }

        private void ShowCompletionMessage()
        {
            string time = _elapsed.ToString(@"mm\:ss");
            var result  = MessageBox.Show(
                $"🎉 Toate etapele au fost finalizate!\n\nTimp total: {time}\n\nDorești să salvezi rezultatele?",
                "Test finalizat", MessageBoxButton.YesNo, MessageBoxImage.Information);

            // TODO: salvare rezultate
            _dashboard.Show();
            Close();
        }

        // ════════════════════════════════════════════
        //  ANULARE / ÎNCHIDERE
        // ════════════════════════════════════════════

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Ești sigur că vrei să anulezi testul?\nProgresul curent nu va fi salvat.",
                "Anulare test", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                _timer.Stop();
                _dashboard.Show();
                Close();
            }
        }

        private void StartTest_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            if (!_dashboard.IsVisible)
                _dashboard.Show();
        }
    }
}