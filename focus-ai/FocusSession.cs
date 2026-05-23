using Microsoft.Win32;

namespace focus_ai
{
    internal static class FocusSession
    {
        private const string RegPath = @"Software\FocusAI";

        public static string Get(string key)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RegPath);
                return k?.GetValue(key)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        public static void Set(string key, string value)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RegPath);
                k.SetValue(key, value);
            }
            catch { }
        }

        public static string DoctorId => Get("Uid");
        public static string IdToken => Get("IdToken");
        public static string Email => Get("Email");

        public static string ActivePatientId
        {
            get => Get("ActivePatientId");
            set => Set("ActivePatientId", value);
        }

        public static string DataOwnerId
        {
            get
            {
                string patientId = ActivePatientId;
                return string.IsNullOrWhiteSpace(patientId) ? DoctorId : patientId;
            }
        }
    }
}
