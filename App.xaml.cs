using System;
using System.Windows;

namespace FloatingReminder
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set shutdown mode to only exit when explicitly told (i.e., from the tray icon)
            // We change this so closing the LoginWindow exits the app,
            // but the MainWindow will set it back to OnExplicitShutdown.
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            // Create and show the new LoginWindow
            var login = new LoginWindow();
            login.Show();
        }
    }
}