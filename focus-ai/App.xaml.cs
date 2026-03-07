using System.Windows;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class App : Application
    {
        private const string RegPath = @"Software\FocusAI";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool firstRun  = IsFirstRun();
            bool loggedIn  = IsLoggedIn();

            if (firstRun)
            {
                MarkNotFirstRun();
                new SignUp().Show();
            }
            else if (loggedIn)
            {
                new Dashboard().Show();
            }
            else
            {
                new Login().Show();
            }
        }

        private bool IsFirstRun()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegPath);
                if (key == null) return true;
                return key.GetValue("HasLaunched") as string != "1";
            }
            catch { return true; }
        }

        private void MarkNotFirstRun()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("HasLaunched", "1");
            }
            catch { }
        }

        private bool IsLoggedIn()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegPath);
                if (key == null) return false;
                string loggedIn = key.GetValue("LoggedIn") as string ?? "0";
                string idToken  = key.GetValue("IdToken")  as string ?? "";
                return loggedIn == "1" && !string.IsNullOrEmpty(idToken);
            }
            catch { return false; }
        }
    }
}