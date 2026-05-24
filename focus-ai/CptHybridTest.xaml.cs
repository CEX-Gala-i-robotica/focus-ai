using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace focus_ai
{
    public class CptTrialResult
    {
        public string Stimulus { get; set; } = "";
        public string PreviousStimulus { get; set; } = "";
        public bool IsTarget { get; set; }
        public bool Responded { get; set; }
        public int? ReactionTimeMs { get; set; }
        public string ResponseType { get; set; } = "";
    }

    public class CptHybridResult
    {
        public double Accuracy { get; set; }
        public double HitRate { get; set; }
        public double FalseAlarmRate { get; set; }
        public double MeanReactionTimeMs { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public int FalseAlarms { get; set; }
        public int CorrectRejections { get; set; }
        public string Interpretation { get; set; } = "";
        public List<CptTrialResult> Trials { get; set; } = new();
    }

    public partial class CptHybridTest : Window
    {
        private const int StimulusDurationMs = 500;
        private const int IsiMs = 1500;
        private const int ResponseWindowMs = 1500;
        private const int PostStimulusDelayMs = 250;
        private const int PracticeFeedbackMs = 900;
        private const int TotalTrials = 50;
        private const int PracticeTrials = 10;
        private const double TargetRatio = 0.25;
        private const double AxRatio = 0.20;

        private static readonly string[] LettersPool = { "A", "B", "C", "D", "E", "F", "G", "H", "X" };
        private readonly Random _random = new();
        private readonly Stopwatch _clock = new();
        private readonly CancellationTokenSource _cts = new();

        private List<CptTrialPlan> _practicePlan = new();
        private List<CptTrialPlan> _testPlan = new();
        private readonly List<CptTrialResult> _testResults = new();

        private CptTrialResult? _currentTrial;
        private CptPhase _phase = CptPhase.Instructions;
        private double _stimulusOnsetMs;
        private double _responseDeadlineMs;
        private bool _lateSpaceAttempt;

        public CptHybridResult? Result { get; private set; }

        public CptHybridTest(bool isDark)
        {
            InitializeComponent();
            LanguageManager.Register(this);
            WindowHelper.MoveToSecondMonitor(this);
            ThemeManager.Apply(isDark);

            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(GlobalPreviewKeyDown), true);
            Loaded += (_, _) => Focus();
            Activated += (_, _) => Focus();
            Closing += (_, _) => _cts.Cancel();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != CptPhase.Instructions) return;

            _practicePlan = BuildTrialPlan(PracticeTrials);
            _testPlan = BuildTrialPlan(TotalTrials);
            _testResults.Clear();

            InstructionsPanel.Visibility = Visibility.Collapsed;
            TrialPanel.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = Visibility.Collapsed;

            _clock.Restart();
            await RunCountdownAsync(_cts.Token);
            await RunPhaseAsync(CptPhase.Practice, _practicePlan, _cts.Token);
            await RunCountdownAsync(_cts.Token);
            await RunPhaseAsync(CptPhase.MainTest, _testPlan, _cts.Token);
            ShowResults();
        }

        private async Task RunCountdownAsync(CancellationToken token)
        {
            ProgressText.Text = string.Format(LanguageManager.T("Trial {0} / {1}"), 0, TotalTrials);
            FeedbackText.Text = "";
            StatusText.Text = LanguageManager.T("Get ready");

            for (int value = 3; value >= 1; value--)
            {
                StimulusText.Text = value.ToString();
                StimulusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                await DelayFromNowAsync(1000, token);
            }

            StimulusText.Text = "+";
            await DelayFromNowAsync(400, token);
        }

        private async Task RunPhaseAsync(CptPhase phase, List<CptTrialPlan> plan, CancellationToken token)
        {
            bool isPractice = phase == CptPhase.Practice;
            StatusText.Text = isPractice
                ? LanguageManager.T("Practice: feedback is shown after each response.")
                : LanguageManager.T("Main test: respond only when the target rule is met.");

            double nextTrialStart = _clock.Elapsed.TotalMilliseconds + 600;

            for (int i = 0; i < plan.Count; i++)
            {
                await WaitUntilAsync(nextTrialStart, token);

                var planned = plan[i];
                ProgressText.Text = string.Format(LanguageManager.T("Trial {0} / {1}"), i + 1, plan.Count);

                StimulusText.Text = "+";
                FeedbackText.Text = "";
                StatusText.Text = isPractice ? LanguageManager.T("Practice") : LanguageManager.T("Main test");
                await WaitUntilAsync(nextTrialStart + PostStimulusDelayMs, token);

                var trial = new CptTrialResult
                {
                    Stimulus = planned.Stimulus,
                    PreviousStimulus = planned.PreviousStimulus,
                    IsTarget = planned.IsTarget
                };

                _currentTrial = trial;
                _lateSpaceAttempt = false;
                _stimulusOnsetMs = _clock.Elapsed.TotalMilliseconds;
                _responseDeadlineMs = _stimulusOnsetMs + ResponseWindowMs;

                StimulusText.Text = planned.Stimulus;
                StimulusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));

                await WaitUntilAsync(_stimulusOnsetMs + StimulusDurationMs, token);
                StimulusText.Text = "+";

                await WaitUntilAsync(_responseDeadlineMs, token);
                FinalizeTrial(trial);
                _currentTrial = null;

                if (isPractice)
                {
                    ShowPracticeFeedback(trial);
                    await DelayFromNowAsync(PracticeFeedbackMs, token);
                }
                else
                {
                    _testResults.Add(trial);
                }

                nextTrialStart = _stimulusOnsetMs + StimulusDurationMs + PostStimulusDelayMs + IsiMs;
            }
        }

        private void GlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;

            e.Handled = true;

            if (_currentTrial == null) return;
            if (_phase != CptPhase.Practice && _phase != CptPhase.MainTest) return;
            if (_currentTrial.Responded) return;

            double now = _clock.Elapsed.TotalMilliseconds;
            if (now > _responseDeadlineMs)
            {
                _lateSpaceAttempt = true;
                return;
            }

            _currentTrial.Responded = true;
            _currentTrial.ReactionTimeMs = Math.Max(0, (int)Math.Round(now - _stimulusOnsetMs));
        }

        private void ShowPracticeFeedback(CptTrialResult trial)
        {
            string text;
            Color color;

            if (trial.ResponseType == "HIT" || trial.ResponseType == "CORRECT_REJECTION")
            {
                text = LanguageManager.T("CORRECT");
                color = Color.FromRgb(34, 197, 94);
            }
            else if (trial.ResponseType == "MISS" && _lateSpaceAttempt)
            {
                text = LanguageManager.T("TOO SLOW");
                color = Color.FromRgb(245, 158, 11);
            }
            else if (trial.ResponseType == "MISS")
            {
                text = LanguageManager.T("MISSED TARGET");
                color = Color.FromRgb(239, 68, 68);
            }
            else
            {
                text = LanguageManager.T("DO NOT PRESS");
                color = Color.FromRgb(239, 68, 68);
            }

            FeedbackText.Text = text;
            FeedbackText.Foreground = new SolidColorBrush(color);
        }

        private void ShowResults()
        {
            TrialPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Visible;
            Result = BuildResult(_testResults);

            ResultAccuracyText.Text = $"{Result.Accuracy:F1}%";
            ResultMeanRtText.Text = Result.MeanReactionTimeMs > 0 ? $"{Result.MeanReactionTimeMs:F0} ms" : "N/A";
            ResultHitRateText.Text = $"{Result.HitRate:F2}";
            ResultFalseAlarmRateText.Text = $"{Result.FalseAlarmRate:F2}";
            ResultHitsText.Text = Result.Hits.ToString();
            ResultMissesText.Text = Result.Misses.ToString();
            ResultFalseAlarmsText.Text = Result.FalseAlarms.ToString();
            ResultCorrectRejectionsText.Text = Result.CorrectRejections.ToString();
            ResultInterpretationText.Text = Result.Interpretation;
        }

        private static void FinalizeTrial(CptTrialResult trial)
        {
            trial.ResponseType = (trial.IsTarget, trial.Responded) switch
            {
                (true, true) => "HIT",
                (true, false) => "MISS",
                (false, true) => "FALSE_ALARM",
                _ => "CORRECT_REJECTION"
            };
        }

        private static CptHybridResult BuildResult(List<CptTrialResult> trials)
        {
            int hits = trials.Count(t => t.ResponseType == "HIT");
            int misses = trials.Count(t => t.ResponseType == "MISS");
            int falseAlarms = trials.Count(t => t.ResponseType == "FALSE_ALARM");
            int correctRejections = trials.Count(t => t.ResponseType == "CORRECT_REJECTION");
            int totalTargets = hits + misses;
            int totalNonTargets = falseAlarms + correctRejections;
            double accuracy = trials.Count > 0 ? (double)(hits + correctRejections) / trials.Count * 100.0 : 0;
            double meanRt = trials
                .Where(t => t.ResponseType == "HIT" && t.ReactionTimeMs.HasValue)
                .Select(t => (double)t.ReactionTimeMs!.Value)
                .DefaultIfEmpty(0)
                .Average();

            var result = new CptHybridResult
            {
                Hits = hits,
                Misses = misses,
                FalseAlarms = falseAlarms,
                CorrectRejections = correctRejections,
                Accuracy = accuracy,
                HitRate = totalTargets > 0 ? (double)hits / totalTargets : 0,
                FalseAlarmRate = totalNonTargets > 0 ? (double)falseAlarms / totalNonTargets : 0,
                MeanReactionTimeMs = meanRt,
                Trials = trials
            };
            result.Interpretation = BuildInterpretation(result);
            return result;
        }

        private static string BuildInterpretation(CptHybridResult result)
        {
            var parts = new List<string>();

            if (result.Accuracy >= 85 && result.MeanReactionTimeMs > 0 && result.MeanReactionTimeMs <= 650)
                parts.Add(LanguageManager.T("High accuracy + low RT: good attention and efficient processing."));
            if (result.FalseAlarms >= 10 || result.FalseAlarmRate >= 0.15)
                parts.Add(LanguageManager.T("Many false alarms: increased impulsivity / reduced inhibitory control."));
            if (result.Misses >= 10 || result.HitRate < 0.75)
                parts.Add(LanguageManager.T("Many misses: possible sustained attention deficit."));
            if (result.MeanReactionTimeMs >= 900)
                parts.Add(LanguageManager.T("Very high RT: slow processing or hesitation in maintaining the rule."));

            return parts.Count > 0
                ? string.Join(Environment.NewLine, parts)
                : LanguageManager.T("Performance is within a good range for this task: stable responses, low errors and adequate pace.");
        }

        private List<CptTrialPlan> BuildTrialPlan(int count)
        {
            int totalX = Math.Max(1, (int)Math.Round(count * TargetRatio));
            int axTargets = Math.Min(totalX, Math.Max(1, (int)Math.Round(count * AxRatio)));
            int nonTargetX = Math.Max(0, totalX - axTargets);

            var stimuli = Enumerable.Range(0, count)
                .Select(_ => NonXLetter(allowA: true))
                .ToList();

            var used = new HashSet<int>();
            for (int i = 0; i < axTargets; i++)
            {
                int xIndex = PickIndex(1, count, used);
                stimuli[xIndex - 1] = "A";
                stimuli[xIndex] = "X";
                used.Add(xIndex - 1);
                used.Add(xIndex);
            }

            for (int i = 0; i < nonTargetX; i++)
            {
                int xIndex = PickIndex(1, count, used);
                stimuli[xIndex - 1] = NonXLetter(allowA: false);
                stimuli[xIndex] = "X";
                used.Add(xIndex - 1);
                used.Add(xIndex);
            }

            var plan = new List<CptTrialPlan>();
            for (int i = 0; i < stimuli.Count; i++)
            {
                string previous = i > 0 ? stimuli[i - 1] : "";
                string stimulus = stimuli[i];
                plan.Add(new CptTrialPlan(stimulus, previous, stimulus == "X" && previous == "A"));
            }

            return plan;
        }

        private int PickIndex(int minInclusive, int maxExclusive, HashSet<int> used)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                int index = _random.Next(minInclusive, maxExclusive);
                if (!used.Contains(index) && !used.Contains(index - 1))
                    return index;
            }

            for (int index = minInclusive; index < maxExclusive; index++)
            {
                if (!used.Contains(index) && !used.Contains(index - 1))
                    return index;
            }

            return _random.Next(minInclusive, maxExclusive);
        }

        private string NonXLetter(bool allowA)
        {
            var pool = LettersPool.Where(l => l != "X" && (allowA || l != "A")).ToArray();
            return pool[_random.Next(pool.Length)];
        }
        

        private async Task DelayFromNowAsync(double durationMs, CancellationToken token)
            => await WaitUntilAsync(_clock.Elapsed.TotalMilliseconds + durationMs, token);

        private async Task WaitUntilAsync(double targetMs, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                double remaining = targetMs - _clock.Elapsed.TotalMilliseconds;
                if (remaining <= 1) return;
                await Task.Delay((int)Math.Min(remaining, 16), token);
            }
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Result != null;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }

        private enum CptPhase
        {
            Instructions,
            Countdown,
            Practice,
            PracticeFeedback,
            MainTest,
            Fixation,
            Results
        }

        private record CptTrialPlan(string Stimulus, string PreviousStimulus, bool IsTarget);
    }
}
