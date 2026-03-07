using System.Windows;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class Dashboard : Window
    {
        private const string RegPath = @"Software\FocusAI";

        public Dashboard()
        {
            InitializeComponent();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSession();
            new Login().Show();
            Close();
        }

        private void ClearSession()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("LoggedIn", "0");
                key.SetValue("IdToken",  "");
                key.SetValue("RememberMe", "0");
                key.SetValue("Email", "");
            }
            catch { }
        }
    }
}