using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
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
        private readonly string _dbUrl = AppSettings.Get("RealtimeDatabaseUrl");
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

                PredictionEngineResolution resolution = await PredictionModelService.GetEngineAsync(history, "patient:" + _patientId);
                var result = resolution.Engine.Predict(history, horizon: 1);
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
                double storedScore = v.TryGetProperty("scor", out var sv) && sv.ValueKind == JsonValueKind.Number ? sv.GetDouble() : 0;
                string mapRaw = v.TryGetProperty("map", out var mp) ? mp.GetString() ?? "" : "";
                SessionFeatures features = FeatureExtractor.FromJsonElement(v);
                double scor = features.HasScoreData ? features.Score : storedScore;
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
                Color = OxyColor.FromRgb(201, 196, 255),
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                StrokeThickness = 1.5
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
                MarkerSize = 6,
                MarkerFill = OxyColor.FromRgb(201, 196, 255),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 1
            };
            predictionPoint.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(predictedDate), prediction.PredictedScore));
            plotModel.Series.Add(predictionPoint);

            var intervalLow = new LineSeries
            {
                Title = LanguageManager.T("Prediction interval (low)"),
                Color = OxyColor.FromArgb(80, 201, 196, 255),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            var intervalHigh = new LineSeries
            {
                Title = LanguageManager.T("Prediction interval (high)"),
                Color = OxyColor.FromArgb(80, 201, 196, 255),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            intervalLow.Points.Add(new DataPoint(DateTimeAxis.ToDouble(predictedDate), prediction.PredictionIntervalLow));
            intervalHigh.Points.Add(new DataPoint(DateTimeAxis.ToDouble(predictedDate), prediction.PredictionIntervalHigh));
            plotModel.Series.Add(intervalLow);
            plotModel.Series.Add(intervalHigh);

            TrendPlot.Model = plotModel;
            PredictionAnnotation.Text = string.Format(
                UiCulture,
                LanguageManager.T("Prediction based on {0} sessions. The interval is empirical only when sufficient historical evaluation data are available. Last score: {1:F1} -> expected score: {2:F1}."),
                history.Count,
                history.Last().Score,
                prediction.PredictedScore);
        }

        private void UpdateStats(List<SessionFeatures> history, PredictionResult prediction)
        {
            PredictedScoreValue.Text = FormatNumber(prediction.PredictedScore, "F1");
            PredictionRange.Text = $"{FormatNumber(prediction.PredictionIntervalLow, "F1")} – {FormatNumber(prediction.PredictionIntervalHigh, "F1")}";
            TrendLabel.Text = LanguageManager.T(prediction.TrendLabel);
            TrendPerSession.Text = $"{FormatNumber(prediction.TrendPerSession, "+0.##;-0.##;0")} {LanguageManager.T("points/session")}";
            IntervalStatusLabel.Text = BuildIntervalStatus(prediction);
            MAEValue.Text = BuildMaeLabel(prediction.ModelMAE);
            SessionsCount.Text = prediction.SessionsUsed.ToString();

            LinearRegrScore.Text = FormatNumber(prediction.LinearRegrScore, "F1");
            HoltScore.Text = FormatNumber(prediction.HoltScore, "F1");
            KnnScore.Text = FormatNumber(prediction.KnnScore, "F1");

            EnsembleExplanation.Text = BuildModelExplanation(prediction);
        }

        private static CultureInfo UiCulture => LanguageManager.Current == AppLanguage.Romanian
            ? CultureInfo.GetCultureInfo("ro-RO")
            : CultureInfo.GetCultureInfo("en-US");

        private static string FormatNumber(double value, string format)
            => value.ToString(format, UiCulture);

        private static string BuildIntervalStatus(PredictionResult prediction)
        {
            string status = LanguageManager.T(prediction.IntervalStatusLabel);
            if (!prediction.TestIntervalCoverage90.HasValue)
                return status;

            string coverage = prediction.TestIntervalCoverage90.Value.ToString("P0", UiCulture);
            return LanguageManager.Current == AppLanguage.Romanian
                ? $"{status} · acoperire pe setul de validare: {coverage}"
                : $"{status} · holdout coverage: {coverage}";
        }

        private static string BuildMaeLabel(double? mae)
        {
            if (mae.HasValue)
                return $"MAE: {FormatNumber(mae.Value, "F2")}";

            return LanguageManager.Current == AppLanguage.Romanian
                ? "MAE: indisponibil"
                : "MAE: n/a";
        }

        private static string BuildModelExplanation(PredictionResult prediction)
        {
            string mae = prediction.ModelMAE.HasValue
                ? FormatNumber(prediction.ModelMAE.Value, "F2")
                : LanguageManager.Current == AppLanguage.Romanian ? "indisponibil" : "not available";

            return LanguageManager.Current == AppLanguage.Romanian
                ? $"Model selectat: {prediction.ModelName}. MAE istoric: {mae}. Eroarea raportată și intervalul de predicție descriu evaluările anterioare și nu garantează rezultatul următor."
                : $"Selected model: {prediction.ModelName}. Historical MAE: {mae}. The reported error and prediction interval describe past evaluation results and do not guarantee the next result.";
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
                .Replace("Low SpO2", LanguageManager.T("Low SpO2"), StringComparison.Ordinal)
                .Replace("Abnormal heart rate", LanguageManager.T("Abnormal heart rate"), StringComparison.Ordinal)
                .Replace("High dispersion in heart-rate samples", LanguageManager.T("High dispersion in heart-rate samples"), StringComparison.Ordinal)
                .Replace("mean", LanguageManager.Current == AppLanguage.Romanian ? "medie" : "mean", StringComparison.Ordinal)
                .Replace("fraction below", LanguageManager.Current == AppLanguage.Romanian ? "proporție sub" : "fraction below", StringComparison.Ordinal)
                .Replace("standard deviation", LanguageManager.Current == AppLanguage.Romanian ? "deviație standard" : "standard deviation", StringComparison.Ordinal);
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
                string value = k?.GetValue(key)?.ToString() ?? "";
                return key == "IdToken" ? SessionTokenProtector.Unprotect(value) : value;
            }
            catch { return ""; }
        }

        private record TestEntry(string Id, string DateTime, string Duration, double Scor, string MapRaw, SessionFeatures? Features);
    }
}
