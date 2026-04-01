using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace focus_ai
{
    public partial class App : Application
    {
        private const string RegPath = @"Software\FocusAI";

        #region WinAPI

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int  cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITORINFOF_PRIMARY = 0x00000001;

        #endregion

        protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    bool isDark = IsSystemDarkTheme();
    ThemeManager.Apply(isDark);

    bool firstRun = IsFirstRun();
    bool loggedIn = IsLoggedIn();

    Window window;
    if (firstRun)
    {
        MarkNotFirstRun();
        window = new SignUp();
    }
    else if (loggedIn)
    {
        window = new Dashboard();
    }
    else
    {
        window = new Login();
    }

    // Setăm monitorul DUPĂ ce fereastra e complet încărcată
    window.Loaded += (s, args) => MoveToSecondMonitor((Window)s);
    window.Show();
}

private static void MoveToSecondMonitor(Window window)
{
    var monitors = new List<MONITORINFO>();

    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
        (IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (GetMonitorInfo(hMonitor, ref info))
                monitors.Add(info);
            return true;
        },
        IntPtr.Zero);

    if (monitors.Count < 2) return;

    MONITORINFO? secondary = null;
    foreach (var m in monitors)
    {
        if ((m.dwFlags & MONITORINFOF_PRIMARY) == 0)
        {
            secondary = m;
            break;
        }
    }

    if (secondary == null) return;

    var screen = secondary.Value.rcWork;

    var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
    uint dpi = GetDpiForWindow(hwnd);
    double scale = dpi / 96.0;

    // Dacă fereastra e maximizată, o restaurăm, mutăm, apoi maximizăm pe monitorul corect
    var previousState = window.WindowState;
    window.WindowState = WindowState.Normal;

    window.WindowStartupLocation = WindowStartupLocation.Manual;
    window.Left = screen.left / scale;
    window.Top  = screen.top  / scale;

    // Re-maximizăm pe monitorul secundar (dacă era maximizată)
    if (previousState == WindowState.Maximized)
        window.WindowState = WindowState.Maximized;
}

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

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

        private static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                return val is int i && i == 0;
            }
            catch { return true; }
        }
    }
}