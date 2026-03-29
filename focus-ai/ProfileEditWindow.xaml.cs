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
        private bool _docEmailOk = true;
        private bool _docPhoneOk = true;

        public ProfileData? Result { get; private set; }
        public bool IsSetupMode { get; private set; }

        public ProfileEditWindow(bool isDark, bool isSetupMode = false)
        {
            InitializeComponent();
            IsSetupMode = isSetupMode;

            ThemeManager.Apply(isDark);

            SubHeaderEmail.Text = GetReg("Email");

            if (isSetupMode)
            {
                this.Title = "Configurează profilul";
                HeaderTitle.Text = "Configurează profilul";
                SaveBtn.Content = "Continuă";
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
                string url = $"{baseUrl}/{userId}/profile.json?auth={token}";

                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                if (string.IsNullOrEmpty(response) || response == "null")
                    return;

                var profile = JsonSerializer.Deserialize<ProfileData>(response);
                if (profile == null) return;

                Dispatcher.Invoke(() =>
                {
                    BoxName.Text        = profile.Name        ?? "";
                    BoxSurname.Text     = profile.Surname     ?? "";
                    BoxPhone.Text       = profile.Phone       ?? "";
                    BoxDoctorEmail.Text = profile.DoctorEmail ?? "";
                    BoxDoctorPhone.Text = profile.DoctorPhone ?? "";

                    if (DateTime.TryParseExact(
                            profile.BirthDate, "dd.MM.yyyy",
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

        private void DoctorEmail_Changed(object sender, TextChangedEventArgs e)
        {
            string val  = BoxDoctorEmail.Text.Trim();
            _docEmailOk = string.IsNullOrEmpty(val) || IsValidEmail(val);
            SetFieldState(BoxDoctorEmailBorder, ErrDoctorEmail, _docEmailOk);
        }

        private void DoctorPhone_Changed(object sender, TextChangedEventArgs e)
        {
            string val  = BoxDoctorPhone.Text.Trim();
            _docPhoneOk = string.IsNullOrEmpty(val) || IsValidPhone(val);
            SetFieldState(BoxDoctorPhoneBorder, ErrDoctorPhone, _docPhoneOk);
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

            if (!_nameOk || !_surnameOk || !_birthOk || !_phoneOk || !_docEmailOk || !_docPhoneOk)
                return;

            string birthStr = BirthDatePicker.SelectedDate!.Value.ToString("dd.MM.yyyy");

            var profile = new ProfileData
            {
                Name        = BoxName.Text.Trim(),
                Surname     = BoxSurname.Text.Trim(),
                BirthDate   = birthStr,
                Phone       = BoxPhone.Text.Trim(),
                DoctorEmail = BoxDoctorEmail.Text.Trim(),
                DoctorPhone = BoxDoctorPhone.Text.Trim()
            };

            try
            {
                string userId  = GetReg("Uid");
                string token   = GetReg("IdToken");
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string url = $"{baseUrl}/{userId}/profile.json?auth={token}";

                using HttpClient client = new();
                var json    = JsonSerializer.Serialize(profile);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var resp = await client.PutAsync(url, content);
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Eroare la salvarea în Firebase.", "Eroare",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SaveToRegistry(profile);
            }
            catch
            {
                MessageBox.Show("Eroare la salvarea în Firebase.", "Eroare",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Marchează profilul ca setat
            try
            {
                string userId  = GetReg("Uid");
                string token   = GetReg("IdToken");
                string baseUrl = ConfigurationManager.AppSettings["RealtimeDatabaseUrl"] ?? "";
                string url = $"{baseUrl}/{userId}/profile/setup.json?auth={token}";

                using HttpClient markClient = new();
                var markContent = new StringContent("true", System.Text.Encoding.UTF8, "application/json");
                await markClient.PutAsync(url, markContent);
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
                k.SetValue("BirthDate",   p.BirthDate   ?? "");
                k.SetValue("Phone",       p.Phone       ?? "");
                k.SetValue("DoctorEmail", p.DoctorEmail ?? "");
                k.SetValue("DoctorPhone", p.DoctorPhone ?? "");
            }
            catch { }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}