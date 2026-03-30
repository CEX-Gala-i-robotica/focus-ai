using System.Windows;

namespace focus_ai
{
    public partial class GameSelectionWindow : Window
    {
        private readonly Dashboard _dashboard;
        private readonly bool _isDark;

        public GameSelectionWindow(Dashboard dashboard, bool isDark)
        {
            InitializeComponent();
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
            Close(); // închide fereastra de selecție
            _dashboard.StartGameAndRefresh(gameWindow);
        }
    }
}