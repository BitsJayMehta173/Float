using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms; // <-- Requires project reference to System.Windows.Forms.dll
using System.Drawing;      // <-- Requires project reference to System.Drawing.dll

namespace FloatingNote
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private FloatingNoteWindow _noteWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                // Use a built-in system icon to avoid needing a .ico file
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "Floating Note Dashboard"
            };

            // Setup context menu for the tray icon
            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("Show Dashboard", OnShowDashboard);
            contextMenu.MenuItems.Add("-"); // Separator
            contextMenu.MenuItems.Add("Exit", OnExitApplication);
            _notifyIcon.ContextMenu = contextMenu;

            // Handle click to show dashboard
            _notifyIcon.Click += OnShowDashboard;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            // 1. Validate and parse settings
            if (!int.TryParse(IntervalInput.Text, out int interval) || interval <= 0)
            {
                ShowError("Interval must be a positive number.");
                return;
            }

            if (!double.TryParse(FontSizeInput.Text, out double fontSize) || fontSize <= 0)
            {
                ShowError("Font size must be a positive number.");
                return;
            }

            var texts = TextArrayInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Where(line => !string.IsNullOrWhiteSpace(line))
                                          .Select(line => line.Trim())
                                          .ToArray();

            if (texts.Length == 0)
            {
                ShowError("Please enter at least one message.");
                return;
            }

            // 2. Create settings object
            var settings = new Settings
            {
                Texts = texts,
                IntervalSeconds = interval,
                StartFontSize = fontSize,
                IsGlowEnabled = GlowCheckBox.IsChecked == true
            };

            // 3. Close existing note window if it's open
            _noteWindow?.Close();

            // 4. Create and show the new note window
            _noteWindow = new FloatingNoteWindow(settings);
            _noteWindow.Show();

            // 5. Hide the dashboard
            this.Hide();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void OnShowDashboard(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
        }

        private void OnExitApplication(object sender, EventArgs e)
        {
            // Clean up
            _notifyIcon.Dispose();
            _noteWindow?.Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            OnExitApplication(null, EventArgs.Empty);
        }

        // FIX APPLIED HERE: Renamed to match XAML 'Closing' event
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Don't close the app when the 'X' is clicked.
            // Instead, just hide the dashboard. The tray 'Exit' is the only way to close.
            e.Cancel = true;
            this.Hide();
        }
    }
}