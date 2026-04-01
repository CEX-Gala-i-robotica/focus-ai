using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace focus_ai
{
    public partial class SignUp : Window
    {
        private readonly string FirebaseApiKey     = ConfigurationManager.AppSettings["FirebaseApiKey"];
        private readonly string GoogleClientId     = ConfigurationManager.AppSettings["GoogleClientId"];
        private readonly string GoogleClientSecret = ConfigurationManager.AppSettings["GoogleClientSecret"];

        private const string RegPath = @"Software\FocusAI";

        public SignUp()
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            ApplySystemTheme();
        }

        private bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0;
            }
            catch { return false; }
        }

        private void ApplySystemTheme()
        {
            if (IsSystemDarkTheme()) ApplyDark();
            else ApplyLight();
        }

        private void ApplyLight()
        {
            WindowBackground.Color = (Color)FindResource("LightWindowBg");
            SetBrush("CardBgBrush",      "LightCardBg");
            SetBrush("TextPrimaryBrush", "LightTextPrimary");
            SetBrush("TextSecBrush",     "LightTextSecondary");
            SetBrush("BorderBrush",      "LightBorder");
            SetBrush("InputBgBrush",     "LightInputBg");
            SetBrush("DividerBrush",     "LightDivider");
            CardShadow.Color   = Colors.Black;
            CardShadow.Opacity = 0.12;
        }

        private void ApplyDark()
        {
            WindowBackground.Color = (Color)FindResource("DarkWindowBg");
            SetBrush("CardBgBrush",      "DarkCardBg");
            SetBrush("TextPrimaryBrush", "DarkTextPrimary");
            SetBrush("TextSecBrush",     "DarkTextSecondary");
            SetBrush("BorderBrush",      "DarkBorder");
            SetBrush("InputBgBrush",     "DarkInputBg");
            SetBrush("DividerBrush",     "DarkDivider");
            CardShadow.Color   = Colors.Black;
            CardShadow.Opacity = 0.50;
        }

        private void SetBrush(string brushKey, string colorKey)
        {
            Resources[brushKey] = new SolidColorBrush((Color)FindResource(colorKey));
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string email    = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Email și parola sunt obligatorii.", "Atenție",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Parola trebuie să aibă cel puțin 6 caractere.", "Atenție",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUiBusy(true);

            try
            {
                using var client = new HttpClient();
                var url  = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={FirebaseApiKey}";
                var body = new { email, password, returnSecureToken = true };

                var response = await client.PostAsync(url,
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Cont creat cu succes! Te poți autentifica acum.", "Succes",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    new Login().Show();
                    Close();
                }
                else
                {
                    var error = JObject.Parse(responseJson);
                    MessageBox.Show(error["error"]?["message"]?.ToString(), "Eroare înregistrare",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetUiBusy(false); }
        }

        private async void GoogleSignUpButton_Click(object sender, RoutedEventArgs e)
        {
            SetUiBusy(true);

            try
            {
                string idToken = await GetGoogleIdTokenAsync();
                using var client = new HttpClient();
                var url  = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={FirebaseApiKey}";
                var body = new
                {
                    postBody          = $"id_token={idToken}&providerId=google.com",
                    requestUri        = "http://localhost",
                    returnSecureToken = true
                };

                var response = await client.PostAsync(url,
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var parsed = JObject.Parse(responseJson);
                    string email         = parsed["email"]?.ToString() ?? "";
                    string firebaseToken = parsed["idToken"]?.ToString() ?? "";
                    string uid           = parsed["localId"]?.ToString() ?? "";

                    SaveSession(email, firebaseToken, uid);

                    await OpenDashboardOrSetup(firebaseToken, uid);
                    Close();
                }
                else
                {
                    MessageBox.Show("Autentificare Google eșuată.", "Eroare",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetUiBusy(false); }
        }

        private void SaveSession(string email, string idToken, string uid)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("RememberMe", "1");
                key.SetValue("Email",      email);
                key.SetValue("LoggedIn",   "1");
                key.SetValue("IdToken",    idToken);
                key.SetValue("Uid",        uid);
            }
            catch { }
        }

        private async Task<string> GetGoogleIdTokenAsync()
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId     = GoogleClientId,
                    ClientSecret = GoogleClientSecret
                },
                Scopes = new[] { "openid", "email", "profile" }
            });

            var credential = await new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver())
                .AuthorizeAsync("user", CancellationToken.None);

            return credential.Token.IdToken;
        }

        private void SetUiBusy(bool busy)
        {
            SignUpButton.IsEnabled       = !busy;
            GoogleSignUpButton.IsEnabled = !busy;
            SignUpButton.Content         = busy ? "Se procesează…" : "Înregistrare";
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            new Login().Show();
            Close();
        }
        private async Task OpenDashboardOrSetup(string token, string uid)
        {
            bool needsSetup = await CheckNeedsProfileSetup(token, uid);

            if (needsSetup)
            {
                bool isDark = IsSystemDarkTheme();
                var setup = new ProfileEditWindow(isDark, isSetupMode: true);
                setup.Show();
            }
            else
            {
                new Dashboard().Show();
            }
        }

        private async Task<bool> CheckNeedsProfileSetup(string token, string uid)
        {
            try
            {
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string url = $"{baseUrl}/{uid}/profile/setup.json?auth={token}";

                using var client = new HttpClient();
                string response = await client.GetStringAsync(url);

                // setup == false sau null => trebuie configurare
                if (response == "null" || response == "false" || string.IsNullOrWhiteSpace(response))
                    return true;

                return false;
            }
            catch
            {
                return false; // la eroare, mergi la Dashboard
            }
        }
    }
}