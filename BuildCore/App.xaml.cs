using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace BuildCore
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private const string AppUserModelId = "BuildCore.BuildCore";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(
            string appID);

        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            // Give the unpackaged process a stable Windows application identity.
            // This allows the taskbar and shell to associate the running window
            // with the BuildCore executable and its embedded application icon.
            _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
