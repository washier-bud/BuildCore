using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BuildCore
{
    internal sealed class WindowTrayService : IDisposable
    {
        private const int WM_APP = 0x8000;
        private const int WM_TRAY = WM_APP + 1;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int ID_TRAY = 1001;

        private readonly MainWindow _window;
        private readonly IntPtr _windowHandle;
        private readonly WndProc _wndProc;
        private IntPtr _previousWndProc;
        private bool _disposed;

        public WindowTrayService(MainWindow window)
        {
            _window = window;
            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            _wndProc = WindowProc;
            _previousWndProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = ID_TRAY,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAY,
                hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512)
            };

            data.szTip = "BuildCore";
            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAY)
            {
                int notification = unchecked((int)lParam.ToInt64());

                if (notification == WM_LBUTTONDBLCLK)
                {
                    ShowWindow();
                    return IntPtr.Zero;
                }

                if (notification == WM_RBUTTONUP)
                {
                    // Right-click support is intentionally handled by the native
                    // tray icon. The primary action remains double-click to reopen.
                    ShowWindow();
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
        }

        public void ShowWindow()
        {
            if (_disposed)
                return;

            try
            {
                _window.ShowFromTray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BUILDCORE TRAY SHOW ERROR: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = ID_TRAY
            };

            Shell_NotifyIcon(NIM_DELETE, ref data);

            if (_previousWndProc != IntPtr.Zero)
                SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _previousWndProc);
        }

        private const int GWLP_WNDPROC = -4;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public uint dwState;
            public uint dwStateMask;
            public string? szInfo;
            public uint uTimeoutOrVersion;
            public string? szInfoTitle;
            public uint dwInfoFlags;
            public IntPtr hBalloonIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwVersion;
            public uint uGuid;
            public IntPtr hIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr prevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}