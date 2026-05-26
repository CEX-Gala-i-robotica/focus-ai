using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace focus_ai
{
    public static class WindowHelper
    {
        private static WaitingWindow? _waitingWindow;
        private static int _primaryWindows;
        private static int _secondaryContentWindows;

        #region WinAPI

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITORINFOF_PRIMARY = 0x00000001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        #endregion

        /// <summary>
        /// Afiseaza pe monitorul secundar mesajul de asteptare cat timp nu ruleaza o fereastra de test sau exercitiu.
        /// </summary>
        public static void InitializeSecondaryWaitingWindow()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(UpdateWaitingWindow));
        }

        /// <summary>
        /// Apeleaza asta pentru ferestrele pacientului: teste si exercitii.
        /// </summary>
        public static void MoveToSecondMonitor(Window window)
        {
            bool tracked = false;

            window.Loaded += (s, e) =>
            {
                if (!tracked)
                {
                    tracked = true;
                    _secondaryContentWindows++;
                    UpdateWaitingWindow();
                }

                ApplyMove((Window)s, primary: false, maximize: false);
            };

            window.Closed += (_, _) =>
            {
                if (!tracked) return;
                tracked = false;
                _secondaryContentWindows = Math.Max(0, _secondaryContentWindows - 1);
                UpdateWaitingWindow();
            };
        }

        /// <summary>
        /// Apeleaza asta pentru ferestrele medicului, ca sa ramana pe monitorul principal.
        /// </summary>
        public static void MoveToPrimaryMonitor(Window window)
        {
            bool tracked = false;

            window.Loaded += (s, e) =>
            {
                if (!tracked)
                {
                    tracked = true;
                    _primaryWindows++;
                    UpdateWaitingWindow();
                }

                ApplyMove((Window)s, primary: true, maximize: false);
            };

            window.Closed += (_, _) =>
            {
                if (!tracked) return;
                tracked = false;
                _primaryWindows = Math.Max(0, _primaryWindows - 1);
                UpdateWaitingWindow();
            };
        }

        public static async Task MoveProcessWindowsToSecondMonitorAsync(Process process, int timeoutMs = 8000)
        {
            _secondaryContentWindows++;
            UpdateWaitingWindow();

            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (!process.HasExited && DateTime.UtcNow < deadline)
                {
                    bool moved = MoveProcessWindowsToSecondMonitor(process.Id);
                    if (moved) return;
                    await Task.Delay(200);
                }
            }
            finally
            {
                if (process.HasExited)
                {
                    _secondaryContentWindows = Math.Max(0, _secondaryContentWindows - 1);
                    UpdateWaitingWindow();
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await process.WaitForExitAsync();
                        }
                        catch { }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _secondaryContentWindows = Math.Max(0, _secondaryContentWindows - 1);
                            UpdateWaitingWindow();
                        });
                    });
                }
            }
        }

        private static void UpdateWaitingWindow()
        {
            if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted)
                return;

            if (_primaryWindows == 0 && _secondaryContentWindows == 0)
            {
                _waitingWindow?.Close();
                _waitingWindow = null;
                return;
            }

            if (GetMonitor(primary: false) == null)
            {
                _waitingWindow?.Close();
                _waitingWindow = null;
                return;
            }

            if (_secondaryContentWindows > 0)
            {
                _waitingWindow?.Hide();
                return;
            }

            if (_waitingWindow == null)
            {
                _waitingWindow = new WaitingWindow();
                _waitingWindow.Closed += (_, _) => _waitingWindow = null;
            }

            if (!_waitingWindow.IsVisible)
                _waitingWindow.Show();

            ApplyMove(_waitingWindow, primary: false, maximize: true);
        }

        private static void ApplyMove(Window window, bool primary, bool maximize)
        {
            var monitor = GetMonitor(primary);
            if (monitor == null) return;

            var screen = monitor.Value.rcWork;
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            double scale = GetDpiForWindow(hwnd) / 96.0;

            var previousState = window.WindowState;
            window.WindowState = WindowState.Normal;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = screen.left / scale;
            window.Top = screen.top / scale;
            if (maximize)
            {
                window.Width = Math.Max(1, (screen.right - screen.left) / scale);
                window.Height = Math.Max(1, (screen.bottom - screen.top) / scale);
            }

            if (maximize || previousState == WindowState.Maximized)
                window.WindowState = WindowState.Maximized;
        }

        private static MONITORINFO? GetMonitor(bool primary)
        {
            var monitors = new List<MONITORINFO>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                    if (GetMonitorInfo(hMonitor, ref info))
                        monitors.Add(info);
                    return true;
                },
                IntPtr.Zero);

            if (monitors.Count == 0) return null;

            foreach (var monitor in monitors)
            {
                bool isPrimary = (monitor.dwFlags & MONITORINFOF_PRIMARY) != 0;
                if (isPrimary == primary)
                    return monitor;
            }

            return primary ? monitors[0] : null;
        }

        private static bool MoveProcessWindowsToSecondMonitor(int processId)
        {
            var monitor = GetMonitor(primary: false);
            if (monitor == null) return false;

            var screen = monitor.Value.rcWork;
            bool moved = false;

            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                if (windowProcessId == processId && IsWindowVisible(hWnd))
                {
                    SetWindowPos(hWnd, IntPtr.Zero,
                        screen.left,
                        screen.top,
                        screen.right - screen.left,
                        screen.bottom - screen.top,
                        SWP_NOZORDER | SWP_SHOWWINDOW);
                    moved = true;
                }

                return true;
            }, IntPtr.Zero);

            return moved;
        }
    }
}
