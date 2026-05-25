using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using focus_ai.Prediction;
using focus_ai.Prediction.Models;

namespace focus_ai
{
    public partial class TrendAnalysisWindow : Window
    {
        private readonly string _patientId;
        private readonly string _patientName;
        private readonly bool _isDark;
        private readonly string _dbUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
        private static readonly HttpClient _http = new();
        private readonly System.Threading.CancellationTokenSource _cts = new();
        private List<SessionFeatures>? _lastHistory;
        private PredictionResult? _lastPrediction;

        public TrendAnalysisWindow(string patientId, string patientName, bool isDark)
        {
            InitializeComponent();
            _patientId = patientId;
            _patientName = patientName;
            _isDark = isDark;
            ThemeManager.Apply(_isDark);
            LanguageManager.Register(this);
            SetPatientTitle();
            LanguageManager.LanguageChanged += TrendAnalysisWindow_LanguageChanged;
            Closed += (_, _) => LanguageManager.LanguageChanged -= TrendAnalysisWindow_LanguageChanged;
            Loaded += async (s, e) => await LoadTrendDataAsync();
        }

        private void TrendAnalysisWindow_LanguageChanged(object? sender, EventArgs e)
        {
            SetPatientTitle();
            if (_lastHistory != null && _lastPrediction != null)
            {
                RenderPlot(_lastHistory, _lastPrediction);
                UpdateStats(_lastHistory, _lastPrediction);
                DisplayAlerts(_lastPrediction);
            }
        }

        private void SetPatientTitle()
        {
            TitlePatient.Text = $"{LanguageManager.T("Patient:")} {_patientName}";
        }

        private async Task LoadTrendDataAsync()
        {
            try
            {
                var tests = await LoadPatientTestsAsync(_patientId);
                if (tests.Count < 2)
                {
                    MessageBox.Show(LanguageManager.T("At least 2 test sessions are needed for analysis."),
                                    "Focus AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                    return;
                }

                var history = tests
                    .Select(t => t.Features)
                    .Where(f => f != null && f.DateTime != DateTime.MinValue)
                    .Cast<SessionFeatures>()
                    .OrderBy(f => f.DateTime)
                    .ToList();

                if (history.Count < 2)
                {
                    MessageBox.Show(LanguageManager.T("Not enough data could be extracted for prediction."),
                                    "Focus AI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                var engine = new PredictionEngine();
                var result = engine.Predict(history, horizon: 1);
                _lastHistory = history;
                _lastPrediction = result;

                RenderPlot(history, result);
                UpdateStats(history, result);
                DisplayAlerts(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.T("Error loading data:")} {ex.Message}", LanguageManager.T("Error"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task<List<TestEntry>> LoadPatientTestsAsync(string patientId)
        {
            string token = GetReg("IdToken");
            string json = await SafeGetAsync($"{_dbUrl}/patients/{patientId}/testResults.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/testResults/{patientId}.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/tests.json?auth={token}");
            if (string.IsNullOrEmpty(json) || json == "null")
                json = await SafeGetAsync($"{_dbUrl}/{patientId}/testResults.json?auth={token}");

            if (string.IsNullOrEmpty(json) || json == "null")
                return new List<TestEntry>();

            var list = new List<TestEntry>();
            using var doc = JsonDocument.Parse(json);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var v = entry.Value;
                string dt = v.TryGetProperty("dateTime", out var dtv) ? dtv.GetString() ?? "" : "";
                string dur = v.TryGetProperty("duration", out var dv) ? dv.GetString() ?? "" : "";
                double scor = v.TryGetProperty("scor", out var sv) ? sv.GetDouble() : 0;
                string mapRaw = v.TryGetProperty("map", out var mp) ? mp.GetString() ?? "" : "";
                SessionFeatures? features = FeatureExtractor.FromJsonElement(v);
                list.Add(new TestEntry(entry.Name, dt, dur, scor, mapRaw, features));
            }
            return list.OrderBy(t => t.DateTime).ToList();
        }

        private void RenderPlot(List<SessionFeatures> history, PredictionResult prediction)
        {
            var plotModel = new PlotModel { Background = OxyColors.Transparent, TextColor = _isDark ? OxyColors.White : OxyColors.Black };
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd.MM HH:mm",
                Title = LanguageManager.T("Date & time"),
                TitleColor = _isDark ? OxyColors.White : OxyColors.Black,
                AxislineColor = _isDark ? OxyColors.Gray : OxyColors.DarkGray,
                TicklineColor = _isDark ? OxyColors.Gray : OxyColors.DarkGray,
                TextColor = _isDark ? OxyColors.WhiteSmoke : OxyColors.Black
            };
            var scoreAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LanguageManager.T("Test score"),
                Minimum = 0,
                Maximum = 100,
                TitleColor = _isDark ? OxyColors.White : OxyColors.Black,
                AxislineColor = _isDark ? OxyColors.Gray : OxyColors.DarkGray,
                TicklineColor = _isDark ? OxyColors.Gray : OxyColors.DarkGray,
                TextColor = _isDark ? OxyColors.WhiteSmoke : OxyColors.Black
            };
            plotModel.Axes.Add(dateAxis);
            plotModel.Axes.Add(scoreAxis);

            var series = new LineSeries
            {
                Title = LanguageManager.T("Actual score"),
                Color = OxyColor.FromRgb(59, 130, 246),
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                StrokeThickness = 2
            };
            foreach (var point in history)
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(point.DateTime), point.Score));
            plotModel.Series.Add(series);

            var lastDate = history.Last().DateTime;
            var predictedDate = lastDate.AddDays(7);
            var predictionPoint = new ScatterSeries
            {
                Title = LanguageManager.T("Prediction (next session)"),
                MarkerType = MarkerType.Diamond,
                MarkerSize = 8,
                MarkerFill = OxyColor.FromRgb(34, 197, 94),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 1.5
            };
            predictionPoint.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(predictedDate), prediction.PredictedScore));
            plotModel.Series.Add(predictionPoint);

            var confidenceLow = new LineSeries
            {
                Title = LanguageManager.T("Confidence interval (low)"),
                Color = OxyColor.FromArgb(80, 34, 197, 94),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            var confidenceHigh = new LineSeries
            {
                Title = LanguageManager.T("Confidence interval (high)"),
                Color = OxyColor.FromArgb(80, 34, 197, 94),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            confidenceLow.Points.Add(new DataPoint(DateTimeAxis.ToDouble(predictedDate), prediction.ConfidenceLow));
            confidenceHigh.Points.Add(new DataPoint(DateTimeAxis.ToDouble(predictedDate), prediction.ConfidenceHigh));
            plotModel.Series.Add(confidenceLow);
            plotModel.Series.Add(confidenceHigh);

            TrendPlot.Model = plotModel;
            PredictionAnnotation.Text = string.Format(
                LanguageManager.T("Prediction based on {0} sessions. The shaded area represents the confidence interval (80%). Last score: {1:F1} -> expected score: {2:F1}."),
                history.Count,
                history.Last().Score,
                prediction.PredictedScore);
        }

        private void UpdateStats(List<SessionFeatures> history, PredictionResult prediction)
        {
            PredictedScoreValue.Text = $"{prediction.PredictedScore:F1}";
            ConfidenceRange.Text = $"{prediction.ConfidenceLow:F1} – {prediction.ConfidenceHigh:F1}";
            TrendLabel.Text = LanguageManager.T(prediction.TrendLabel);
            TrendPerSession.Text = $"{prediction.TrendPerSession:+0.##;-0.##;0} {LanguageManager.T("points/session")}";
            ConfidenceLevel.Text = LanguageManager.T(prediction.ConfidenceLabel);
            MAEValue.Text = $"MAE: {prediction.ModelMAE:F2}";
            SessionsCount.Text = prediction.SessionsUsed.ToString();

            LinearRegrScore.Text = $"{prediction.LinearRegrScore:F1}";
            HoltScore.Text = $"{prediction.HoltScore:F1}";
            KnnScore.Text = $"{prediction.KnnScore:F1}";

            string explanation = string.Format(
                LanguageManager.T("The ensemble model combines linear regression (weight {0}), Holt exponential smoothing, and k-NN based on physiological similarity. Final prediction: {1:F1}."),
                GetWeightDescription(history.Count),
                prediction.PredictedScore);
            EnsembleExplanation.Text = explanation;
        }

        private string GetWeightDescription(int n)
        {
            if (n <= 3) return "15%";
            if (n <= 6) return "25%";
            if (n <= 12) return "35%";
            return "40%";
        }

        private void DisplayAlerts(PredictionResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.AlertMessage))
            {
                AlertMessage.Text = TranslateAlertMessage(result.AlertMessage);
                AlertPanel.Visibility = Visibility.Visible;
            }
            else
            {
                AlertPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadTrendDataAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string TranslateAlertMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "";

            return message
                .Replace("Low SpO2", LanguageManager.T("Low SpO2"))
                .Replace("Abnormal heart rate", LanguageManager.T("Abnormal heart rate"))
                .Replace("High heart-rate variability", LanguageManager.T("High heart-rate variability"))
                .Replace("avg", LanguageManager.Current == AppLanguage.Romanian ? "medie" : "avg")
                .Replace("values", LanguageManager.Current == AppLanguage.Romanian ? "valori" : "values");
        }

        private async Task<string> SafeGetAsync(string url)
        {
            try { return await _http.GetStringAsync(url, _cts.Token); }
            catch { return ""; }
        }

        private string GetReg(string key)
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\FocusAI");
                return k?.GetValue(key)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private record TestEntry(string Id, string DateTime, string Duration, double Scor, string MapRaw, SessionFeatures? Features);
    }
}
