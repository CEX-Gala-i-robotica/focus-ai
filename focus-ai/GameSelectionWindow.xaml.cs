using System.Windows;

namespace focus_ai
{
    public partial class GameSelectionWindow : Window
    {
        private readonly Dashboard _dashboard;
        private readonly bool _isDark;
        private bool _activityRunning;

        public GameSelectionWindow(Dashboard dashboard, bool isDark)
        {
            InitializeComponent();
            LanguageManager.Register(this);
            WindowHelper.MoveToPrimaryMonitor(this);
            _dashboard = dashboard;
            _isDark = isDark;
            ThemeManager.Apply(_isDark);
            Owner = dashboard;
        }

        private void GameMemorie_Click(object sender, RoutedEventArgs e)
            => CloseAndStartGame(new MemorieGame(_isDark));

        private void GameStroop_Click(object sender, RoutedEventArgs e)
            => CloseAndStartGame(new StroopGame(_isDark));

        private void GameVisualSearch_Click(object sender, RoutedEventArgs e)
            => CloseAndStartGame(new VisualSearchGame(_isDark));

        private void GameSecvente_Click(object sender, RoutedEventArgs e)
            => CloseAndStartGame(new SecventeGame(_isDark));

        private void GameMatematica_Click(object sender, RoutedEventArgs e)
            => CloseAndStartGame(new MatematicaGame(_isDark));

        private void CloseAndStartGame(Window gameWindow)
        {
            if (_activityRunning) return;

            _activityRunning = true;
            RootGrid.IsEnabled = false;
            gameWindow.Owner = this;
            WindowHelper.MoveToSecondMonitor(gameWindow);
            gameWindow.Closed += (_, _) =>
            {
                _activityRunning = false;
                RootGrid.IsEnabled = true;
                _dashboard.RefreshActivitiesAfterGame();
                Close();
            };
            gameWindow.Show();
        }
    }
}
