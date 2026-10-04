using System;
using System.Drawing;
using System.Windows.Forms;

namespace BuildCore
{
    internal sealed class WindowTrayService : IDisposable
    {
        private readonly MainWindow _window;
        private readonly NotifyIcon _notifyIcon;
        private bool _disposed;

        public WindowTrayService(MainWindow window)
        {
            _window = window;

            var menu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open BuildCore");
            openItem.Click += (_, _) => ShowWindow();

            var exitItem = new ToolStripMenuItem("Exit BuildCore");
            exitItem.Click += (_, _) => _window.ExitFromTray();

            menu.Items.Add(openItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "BuildCore",
                ContextMenuStrip = menu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (_, _) => ShowWindow();
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
                System.Diagnostics.Debug.WriteLine(
                    $"BUILDCORE TRAY SHOW ERROR: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
