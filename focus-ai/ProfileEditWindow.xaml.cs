using System;
using System.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class ProfileEditWindow : Window
    {
        private const string RegPath = @"Software\FocusAI";

        private bool _nameOk     = true;
        private bool _surnameOk  = true;
        private bool _phoneOk    = true;
        private bool _birthOk    = true;
        private bool _addressOk = true;

        public ProfileData? Result { get; private set; }
        public bool IsSetupMode { get; private set; }

        public ProfileEditWindow(bool isDark, bool isSetupMode = false)
        {
            InitializeComponent();
            WindowHelper.MoveToSecondMonitor(this);
            IsSetupMode = isSetupMode;

            ThemeManager.Apply(isDark);
            LanguageToggleBtn.Visibility = isSetupMode ? Visibility.Visible : Visibility.Collapsed;
            LanguageManager.Register(this, isSetupMode ? LanguageToggleBtn : null);

            SubHeaderEmail.Text = GetReg("Email");

            if (isSetupMode)
            {
                this.Title = LanguageManager.T("Set up profile");
                HeaderTitle.Text = LanguageManager.T("Set up profile");
                SaveBtn.Content = LanguageManager.T("Continue");
                CancelBtn.IsEnabled = false;
                CancelBtn.Opacity = 0.4;
            }

            _ = LoadFromFirebase();
        }

        // ═══════════════════════════════════════════════════
        //  FIREBASE — LOAD PROFILE
        // ═══════════════════════════════════════════════════

        private async Task LoadFromFirebase()
        {
            try
            {
                string userId = GetReg("Uid");
                string token  = GetReg("IdToken");

                if (string.IsNullOrEmpty(userId))
                    return;

                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string url = $"{baseUrl}/doctors/{userId}.json?auth={token}";

                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                if (string.IsNullOrEmpty(response) || response == "null")
                {
                    url = $"{baseUrl}/{userId}/profile.json?auth={token}";
                    response = await client.GetStringAsync(url);
                    if (string.IsNullOrEmpty(response) || response == "null")
                        return;
                }

                var profile = JsonSerializer.Deserialize<ProfileData>(response);
                if (profile == null) return;

                Dispatcher.Invoke(() =>
                {
                    BoxName.Text        = profile.Name        ?? "";
                    BoxSurname.Text     = profile.Surname     ?? "";
                    BoxPhone.Text       = profile.EffectivePhone;
                    BoxAddress.Text     = profile.Adress ?? "";

                    if (DateTime.TryParseExact(
                            profile.EffectiveBirthDate, "dd.MM.yyyy",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var bd))
                    {
                        BirthDatePicker.SelectedDate = bd;
                    }

                    UpdateHeaderInitials();
                });
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        //  LIVE VALIDATION
        // ═══════════════════════════════════════════════════

        private void Name_Changed(object sender, TextChangedEventArgs e)
        {
            _nameOk    = BoxName.Text.Trim().Length > 0;
            _surnameOk = BoxSurname.Text.Trim().Length > 0;

            SetFieldState(BoxNameBorder,    ErrName,    _nameOk);
            SetFieldState(BoxSurnameBorder, ErrSurname, _surnameOk);
            UpdateHeaderInitials();
        }

        private void BirthDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            _birthOk = BirthDatePicker.SelectedDate.HasValue;
            BoxBirthBorder.BorderBrush = _birthOk
                ? (SolidColorBrush)FindResource("BgInputBorder")
                : new SolidColorBrush(Colors.Red);
            ErrBirth.Visibility = _birthOk ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Phone_Changed(object sender, TextChangedEventArgs e)
        {
            string val = BoxPhone.Text.Trim();
            _phoneOk   = string.IsNullOrEmpty(val) || IsValidPhone(val);
            SetFieldState(BoxPhoneBorder, ErrPhone, _phoneOk);
        }

        private void Address_Changed(object sender, TextChangedEventArgs e)
        {
            string val  = BoxAddress.Text.Trim();
            _addressOk = val.Length > 0;
            SetFieldState(BoxAddressBorder, ErrAddress, _addressOk);
        }

        // ═══════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════

        private void SetFieldState(Border border, TextBlock errBlock, bool ok)
        {
            border.BorderBrush = ok
                ? (SolidColorBrush)FindResource("BgInputBorder")
                : new SolidColorBrush(Colors.Red);
            errBlock.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateHeaderInitials()
        {
            string n = BoxName.Text.Trim();
            string s = BoxSurname.Text.Trim();
            string initials = "";
            if (n.Length > 0) initials += char.ToUpper(n[0]);
            if (s.Length > 0) initials += char.ToUpper(s[0]);
            HeaderInitials.Text = initials.Length > 0 ? initials : "U";
        }

        private static bool IsValidEmail(string s)
            => Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private static bool IsValidPhone(string s)
            => Regex.IsMatch(s, @"^(\+?\d[\d\s\-\(\)]{6,20})$");

        private string GetReg(string key)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RegPath);
                return k?.GetValue(key)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        // ═══════════════════════════════════════════════════
        //  SAVE / CANCEL
        // ═══════════════════════════════════════════════════

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            _nameOk    = BoxName.Text.Trim().Length > 0;
            _surnameOk = BoxSurname.Text.Trim().Length > 0;
            _birthOk   = BirthDatePicker.SelectedDate.HasValue;

            SetFieldState(BoxNameBorder,    ErrName,    _nameOk);
            SetFieldState(BoxSurnameBorder, ErrSurname, _surnameOk);
            BoxBirthBorder.BorderBrush = _birthOk
                ? (SolidColorBrush)FindResource("BgInputBorder")
                : new SolidColorBrush(Colors.Red);
            ErrBirth.Visibility = _birthOk ? Visibility.Collapsed : Visibility.Visible;

            _addressOk = BoxAddress.Text.Trim().Length > 0;
            SetFieldState(BoxAddressBorder, ErrAddress, _addressOk);

            if (!_nameOk || !_surnameOk || !_birthOk || !_phoneOk || !_addressOk)
                return;

            string birthStr = BirthDatePicker.SelectedDate!.Value.ToString("dd.MM.yyyy");

            var profile = new ProfileData
            {
                Name        = BoxName.Text.Trim(),
                Surname     = BoxSurname.Text.Trim(),
                BirthDate   = birthStr,
                Phone       = BoxPhone.Text.Trim(),
                DoctorEmail = GetReg("Email"),
                Adress = BoxAddress.Text.Trim()
            };

            try
            {
                string userId  = GetReg("Uid");
                string token   = GetReg("IdToken");
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string doctorProfileUrl = $"{baseUrl}/doctors/{userId}.json?auth={token}";
                string legacyUrl = $"{baseUrl}/{userId}/profile.json?auth={token}";

                using HttpClient client = new();
                var doctorPayload = JsonSerializer.Serialize(new
                {
                    name = profile.Name,
                    surname = profile.Surname,
                    birthDate = profile.BirthDate,
                    phone = profile.Phone,
                    adress = profile.Adress,
                    email = GetReg("Email"),
                    setup = true,
                    updatedAt = DateTime.UtcNow.ToString("O")
                });
                var content = new StringContent(doctorPayload, System.Text.Encoding.UTF8, "application/json");

                var resp = await client.PatchAsync(doctorProfileUrl, content);
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(LanguageManager.T("Could not save data to Firebase."), LanguageManager.T("Error"),
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await client.PutAsync(legacyUrl,
                    new StringContent(JsonSerializer.Serialize(profile), System.Text.Encoding.UTF8, "application/json"));

                SaveToRegistry(profile);
            }
            catch
            {
                MessageBox.Show(LanguageManager.T("Could not save data to Firebase."), LanguageManager.T("Error"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Mark profile as configured.
            try
            {
                string userId  = GetReg("Uid");
                string token   = GetReg("IdToken");
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string url = $"{baseUrl}/doctors/{userId}/setup.json?auth={token}";

                using HttpClient markClient = new();
                var markContent = new StringContent("true", System.Text.Encoding.UTF8, "application/json");
                await markClient.PutAsync(url, markContent);
                await markClient.PutAsync($"{baseUrl}/{userId}/profile/setup.json?auth={token}",
                    new StringContent("true", System.Text.Encoding.UTF8, "application/json"));
            }
            catch { }

            if (IsSetupMode)
            {
                new Dashboard().Show();
            }

            Result = profile;
            if (!IsSetupMode)
                DialogResult = true;
            else
                Close();
        }

        private void SaveToRegistry(ProfileData p)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RegPath);
                k.SetValue("Name",        p.Name        ?? "");
                k.SetValue("Surname",     p.Surname     ?? "");
                k.SetValue("BirthDate",   p.EffectiveBirthDate);
                k.SetValue("Phone",       p.EffectivePhone);
                k.SetValue("DoctorEmail", p.DoctorEmail ?? "");
                k.SetValue("Adress", p.Adress ?? "");
            }
            catch { }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
