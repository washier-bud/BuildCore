using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;
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

        private static readonly object SyncRoot = new();

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BuildCore");

        private static string SettingsPath =>
            Path.Combine(SettingsDirectory, "windowstate.json");

        public static void Restore(MainWindow window)
        {
            try
            {
                AppWindow appWindow = GetAppWindow(window);
                Dictionary<string, JsonElement> values = LoadValues();

                if (!TryGetInt(values, PositionXKey, out int restoreX) ||
                    !TryGetInt(values, PositionYKey, out int restoreY) ||
                    !TryGetInt(values, WidthKey, out int restoreWidth) ||
                    !TryGetInt(values, HeightKey, out int restoreHeight))
                {
                    return;
                }

                restoreWidth = Math.Max(restoreWidth, MinimumWidth);
                restoreHeight = Math.Max(restoreHeight, MinimumHeight);

                if (!IsVisibleOnAnyDisplay(
                    restoreX,
                    restoreY,
                    restoreWidth,
                    restoreHeight))
                {
                    return;
                }

                appWindow.Resize(new SizeInt32(restoreWidth, restoreHeight));
                appWindow.Move(new PointInt32(restoreX, restoreY));

                if (TryGetBool(values, MaximizedKey, out bool wasMaximized) &&
                    wasMaximized &&
                    appWindow.Presenter is OverlappedPresenter restorePresenter)
                {
                    restorePresenter.Maximize();
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
            try
            {
                AppWindow appWindow = GetAppWindow(window);
                Dictionary<string, JsonElement> values = LoadValues();

                bool isMaximized =
                    appWindow.Presenter is OverlappedPresenter savePresenter &&
                    savePresenter.State == OverlappedPresenterState.Maximized;

                values[MaximizedKey] = JsonSerializer.SerializeToElement(isMaximized);

                // Keep the last normal bounds intact while maximized. This
                // means a later restore to windowed mode returns to the exact
                // size and position the user had before maximizing.
                if (!isMaximized)
                {
                    SizeInt32 windowSize = appWindow.Size;
                    PointInt32 windowPosition = appWindow.Position;

                    if (windowSize.Width >= MinimumWidth &&
                        windowSize.Height >= MinimumHeight)
                    {
                        values[WidthKey] =
                            JsonSerializer.SerializeToElement(windowSize.Width);
                        values[HeightKey] =
                            JsonSerializer.SerializeToElement(windowSize.Height);
                    }

                    values[PositionXKey] =
                        JsonSerializer.SerializeToElement(windowPosition.X);
                    values[PositionYKey] =
                        JsonSerializer.SerializeToElement(windowPosition.Y);
                }

                SaveValues(values);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"BUILDCORE WINDOW STATE SAVE ERROR: {ex}");
            }
        }

        private static AppWindow GetAppWindow(MainWindow window)
        {
            Microsoft.UI.WindowId windowId =
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                    WindowNative.GetWindowHandle(window));

            return AppWindow.GetFromWindowId(windowId);
        }

        private static bool IsVisibleOnAnyDisplay(
            int windowX,
            int windowY,
            int windowWidth,
            int windowHeight)
        {
            int rightEdge = windowX + windowWidth;
            int bottomEdge = windowY + windowHeight;

            foreach (DisplayArea display in DisplayArea.FindAll())
            {
                RectInt32 workArea = display.WorkArea;

                int intersectionWidth =
                    Math.Min(rightEdge, workArea.X + workArea.Width) -
                    Math.Max(windowX, workArea.X);

                int intersectionHeight =
                    Math.Min(bottomEdge, workArea.Y + workArea.Height) -
                    Math.Max(windowY, workArea.Y);

                if (intersectionWidth >= 100 &&
                    intersectionHeight >= 100)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, JsonElement> LoadValues()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(SettingsPath))
                    {
                        return new Dictionary<string, JsonElement>(
                            StringComparer.Ordinal);
                    }

                    string json = File.ReadAllText(SettingsPath);

                    return JsonSerializer.Deserialize<
                        Dictionary<string, JsonElement>>(json)
                        ?? new Dictionary<string, JsonElement>(
                            StringComparer.Ordinal);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"BUILDCORE WINDOW STATE LOAD ERROR: {ex}");

                    return new Dictionary<string, JsonElement>(
                        StringComparer.Ordinal);
                }
            }
        }

        private static void SaveValues(
            Dictionary<string, JsonElement> values)
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(SettingsDirectory);

                string json = JsonSerializer.Serialize(
                    values,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(SettingsPath, json);
            }
        }

        private static bool TryGetInt(
            Dictionary<string, JsonElement> values,
            string key,
            out int value)
        {
            if (values.TryGetValue(key, out JsonElement raw) &&
                raw.TryGetInt32(out int integer))
            {
                value = integer;
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryGetBool(
            Dictionary<string, JsonElement> values,
            string key,
            out bool value)
        {
            if (values.TryGetValue(key, out JsonElement raw) &&
                raw.ValueKind == JsonValueKind.True ||
                values.TryGetValue(key, out raw) &&
                raw.ValueKind == JsonValueKind.False)
            {
                value = raw.GetBoolean();
                return true;
            }

            value = false;
            return false;
        }
    }
}