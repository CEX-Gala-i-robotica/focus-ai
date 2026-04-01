using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace focus_ai
{
    public static class WindowHelper
    {
        #region WinAPI

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

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

        /// <summary>
        /// Apelează asta în constructorul oricărei ferestre ca să apară pe monitorul secundar.
        /// </summary>
        public static void MoveToSecondMonitor(Window window)
        {
            window.Loaded += (s, e) => ApplyMove((Window)s);
        }

        private static void ApplyMove(Window window)
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
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            double scale = GetDpiForWindow(hwnd) / 96.0;

            var previousState = window.WindowState;
            window.WindowState = WindowState.Normal;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = screen.left / scale;
            window.Top  = screen.top  / scale;

            if (previousState == WindowState.Maximized)
                window.WindowState = WindowState.Maximized;
        }
    }
}