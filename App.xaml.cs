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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Create and show the main dashboard window
            var dashboard = new MainWindow();
            dashboard.Show();
        }
    }
}