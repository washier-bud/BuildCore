using System;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace BuildCore
{
    /// <summary>
    /// Persists and restores the BuildCore main window's normal bounds and
    /// maximized state. Invalid/off-screen bounds are ignored so the window
    /// cannot become permanently inaccessible after a monitor change.
    /// </summary>
    internal static class WindowStateService
    {
        private const string PositionXKey = "WindowPositionX";
        private const string PositionYKey = "WindowPositionY";
        private const string WidthKey = "WindowWidth";
        private const string HeightKey = "WindowHeight";
        private const string MaximizedKey = "WindowMaximized";

        private const int MinimumWidth = 900;
        private const int MinimumHeight = 600;

        public static void Restore(MainWindow window)
        {
            try
            {
                AppWindow appWindow = GetAppWindow(window);
                ApplicationDataContainer store =
                    ApplicationData.Current.LocalSettings;

                if (!TryGetInt(store, PositionXKey, out int x) ||
                    !TryGetInt(store, PositionYKey, out int y) ||
                    !TryGetInt(store, WidthKey, out int width) ||
                    !TryGetInt(store, HeightKey, out int height))
                {
                    return;
                }

                width = Math.Max(width, MinimumWidth);
                height = Math.Max(height, MinimumHeight);

                if (!IsVisibleOnAnyDisplay(x, y, width, height))
                {
                    return;
                }

                appWindow.Resize(new SizeInt32(width, height));
                appWindow.Move(new PointInt32(x, y));

                if (TryGetBool(store, MaximizedKey, out bool maximized) &&
                    maximized)
                {
                    if (appWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.Maximize();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"BUILDCORE WINDOW STATE RESTORE ERROR: {ex}");
            }
        }

        public static void Save(MainWindow window)
        {
            AppWindow appWindow = GetAppWindow(window);
            ApplicationDataContainer store =
                ApplicationData.Current.LocalSettings;

            bool maximized =
                appWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Maximized;

            store.Values[MaximizedKey] = maximized;

            // Keep the last normal bounds intact while maximized. This
            // means a later restore to windowed mode returns to the exact
            // size and position the user had before maximizing.
            if (!maximized)
            {
                SizeInt32 size = appWindow.Size;
                PointInt32 position = appWindow.Position;

                if (size.Width >= MinimumWidth &&
                    size.Height >= MinimumHeight)
                {
                    store.Values[WidthKey] = size.Width;
                    store.Values[HeightKey] = size.Height;
                }

                store.Values[PositionXKey] = position.X;
                store.Values[PositionYKey] = position.Y;
            }
        }

        private static AppWindow GetAppWindow(MainWindow window)
        {
            WindowId windowId =
                Win32Interop.GetWindowIdFromWindow(
                    WindowNative.GetWindowHandle(window));

            return AppWindow.GetFromWindowId(windowId);
        }

        private static bool IsVisibleOnAnyDisplay(
            int x,
            int y,
            int width,
            int height)
        {
            int right = x + width;
            int bottom = y + height;

            foreach (DisplayArea display in DisplayArea.FindAll())
            {
                RectInt32 workArea = display.WorkArea;

                int intersectionWidth =
                    Math.Min(right, workArea.X + workArea.Width) -
                    Math.Max(x, workArea.X);

                int intersectionHeight =
                    Math.Min(bottom, workArea.Y + workArea.Height) -
                    Math.Max(y, workArea.Y);

                if (intersectionWidth >= 100 &&
                    intersectionHeight >= 100)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInt(
            ApplicationDataContainer store,
            string key,
            out int value)
        {
            if (store.Values.TryGetValue(key, out object? raw) &&
                raw is int integer)
            {
                value = integer;
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryGetBool(
            ApplicationDataContainer store,
            string key,
            out bool value)
        {
            if (store.Values.TryGetValue(key, out object? raw) &&
                raw is bool boolean)
            {
                value = boolean;
                return true;
            }

            value = false;
            return false;
        }
    }
}
