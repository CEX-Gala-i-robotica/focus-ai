using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
            string name = NameTextBox.Text.Trim();
            string surname = SurnameTextBox.Text.Trim();
            string phone = PhoneTextBox.Text.Trim();
            string adress = CabinetAddressTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(surname) ||
                !BirthDatePicker.SelectedDate.HasValue || string.IsNullOrEmpty(phone) ||
                string.IsNullOrEmpty(adress) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Fill in all fields for the doctor account.", "Warning",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("The email address is not valid.", "Warning",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidPhone(phone))
            {
                MessageBox.Show("The phone number is not valid.", "Warning",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("The password must be at least 6 characters long.", "Warning",
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
                    var parsed = JObject.Parse(responseJson);
                    string uid = parsed["localId"]?.ToString() ?? "";
                    string token = parsed["idToken"]?.ToString() ?? "";
                    string authEmail = parsed["email"]?.ToString() ?? email;
                    await CreateDoctorShellAsync(uid, authEmail, token, name, surname,
                        BirthDatePicker.SelectedDate!.Value, phone, adress, setup: true);

                    MessageBox.Show("Account created successfully. You can sign in now.", "Success",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    new Login().Show();
                    Close();
                }
                else
                {
                    var error = JObject.Parse(responseJson);
                    MessageBox.Show(error["error"]?["message"]?.ToString(), "Sign-up error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    await CreateDoctorShellAsync(uid, email, firebaseToken, "", "", null, "", "", setup: false);

                    await OpenDashboardOrSetup(firebaseToken, uid);
                    Close();
                }
                else
                {
                    MessageBox.Show("Google sign-in failed.", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            SignUpButton.Content         = busy ? "Processing..." : "Sign up";
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
                string url = $"{baseUrl}/doctors/{uid}/setup.json?auth={token}";

                using var client = new HttpClient();
                string response = await client.GetStringAsync(url);

                if (response == "true") return false;

                string legacyUrl = $"{baseUrl}/{uid}/profile/setup.json?auth={token}";
                string legacyResponse = await client.GetStringAsync(legacyUrl);
                return legacyResponse == "null" || legacyResponse == "false" || string.IsNullOrWhiteSpace(legacyResponse);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidEmail(string s)
            => Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private static bool IsValidPhone(string s)
            => Regex.IsMatch(s, @"^(\+?\d[\d\s\-\(\)]{6,20})$");

        private async Task CreateDoctorShellAsync(string uid, string email, string token,
            string name, string surname, DateTime? birthDate, string phone,
            string adress, bool setup)
        {
            if (string.IsNullOrWhiteSpace(uid)) return;

            try
            {
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                using HttpClient client = new();
                var now = DateTime.UtcNow.ToString("O");

                var doctor = new
                {
                    name,
                    surname,
                    birthDate = birthDate?.ToString("dd.MM.yyyy") ?? "",
                    phone,
                    adress,
                    email,
                    setup,
                    createdAt = now,
                    updatedAt = now
                };

                await client.PatchAsync($"{baseUrl}/doctors/{uid}.json?auth={token}",
                    new StringContent(JsonConvert.SerializeObject(doctor), Encoding.UTF8, "application/json"));
            }
            catch { }
        }
    }
}
