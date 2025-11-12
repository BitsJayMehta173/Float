using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Drawing;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Forms.ContextMenu;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Threading; // NEW: For the DispatcherTimer

namespace FloatingReminder
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private FloatingNoteWindow _noteWindow;
        private int _currentGradientIndex = 0;

        public ObservableCollection<ReminderItem> Reminders { get; set; } = new ObservableCollection<ReminderItem>();
        private ReminderItem _editingItem = null;

        private Settings _settings;

        private readonly string _currentUsername;
        private readonly bool _isGuest;
        private bool _isLoggingOut = false;

        // --- NEW: Timer for auto-sync ---
        private DispatcherTimer _syncTimer;

        public MainWindow(string username, bool isGuest)
        {
            InitializeComponent();

            _currentUsername = username;
            _isGuest = isGuest;

            _settings = SettingsService.LoadSettings(_currentUsername);

            RemindersList.ItemsSource = Reminders;
            RefreshVisualList();

            FontSizeInput.Text = _settings.StartFontSize.ToString();
            GlowCheckBox.IsChecked = _settings.IsGlowEnabled;

            ConfigureUIMode();

            // --- NEW: Start auto-sync timer if we are a logged-in user ---
            if (!_isGuest)
            {
                _syncTimer = new DispatcherTimer();
                _syncTimer.Interval = TimeSpan.FromMinutes(5); // Sync every 5 minutes
                _syncTimer.Tick += OnAutoSyncTimer_Tick;
                _syncTimer.Start();
            }
        }

        // --- NEW: Auto-sync timer event ---
        private async void OnAutoSyncTimer_Tick(object sender, EventArgs e)
        {
            // Run the sync logic, but flag it as "automatic"
            // This will hide "Sync Error" messages and just show "Offline"
            await DoSmartSync(isAutoSync: true);
        }

        // --- NEW: Reusable Smart Sync Logic ---
        private async Task DoSmartSync(bool isAutoSync = false)
        {
            // Don't sync if we're a guest
            if (_isGuest) return;

            // Disable button if it exists
            if (SyncButton != null) SyncButton.IsEnabled = false;

            try
            {
                // 1. Get local items (master list, including deleted)
                var localItems = _settings.Items ?? new List<ReminderItem>();

                // 2. Get cloud items
                var cloudSettings = await MongoSyncService.DownloadSettingsAsync(_currentUsername);
                var cloudItems = cloudSettings?.Items ?? new List<ReminderItem>();

                // 3. Combine and find "winners"
                var allItems = localItems.Concat(cloudItems)
                                         .GroupBy(item => item.Id);

                var finalMergedList = allItems.Select(group =>
                {
                    // Find the item with the latest 'LastModified' date
                    return group.OrderByDescending(item => item.LastModified).First();
                })
                    .ToList();

                // 4. Save the new "master list" everywhere
                _settings.Items = finalMergedList;
                SaveAndRefreshSettings(); // Saves locally
                await MongoSyncService.UploadSettingsAsync(_settings, _currentUsername); // Uploads to cloud

                // 5. Refresh the UI (this will filter out deleted items)
                RefreshVisualList();

                // 6. Report Success
                SetSyncStatus($"✅ Synced ({DateTime.Now:h:mm tt})", "#90EE90");
            }
            catch (Exception ex)
            {
                // 7. Report Failure
                string error = "Offline. (Last sync failed)";
                // Only show a scary error if the user *manually* clicked the button
                if (!isAutoSync) error = "Sync Error. Check connection.";

                SetSyncStatus(error, "#FF6B6B");
                Console.WriteLine($"[SYNC ERROR]: {ex.Message}");
            }
            finally
            {
                if (SyncButton != null) SyncButton.IsEnabled = true;
            }
        }

        // --- NEW: Helper to populate the visual list ---
        private void RefreshVisualList()
        {
            var activeItems = _settings.Items
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.CreatedAt)
                .ToList();

            Reminders.Clear();
            foreach (var item in activeItems)
            {
                Reminders.Add(item);
            }
        }

        private void ConfigureUIMode()
        {
            if (_isGuest)
            {
                TitleText.Text = "Dashboard (Guest)";
                LoginButton.Visibility = Visibility.Visible;
                LogoutButton.Visibility = Visibility.Collapsed;
                SyncButton.Visibility = Visibility.Collapsed;
                LoadButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = $"Dashboard ({_currentUsername})";
                LoginButton.Visibility = Visibility.Collapsed;
                LogoutButton.Visibility = Visibility.Visible;
                SyncButton.Visibility = Visibility.Visible;
                LoadButton.Visibility = Visibility.Collapsed;
            }
        }

        // UPDATED: Now runs a sync on startup
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // NEW: Run one sync on startup for logged-in users
            if (!_isGuest)
            {
                SetSyncStatus("Syncing...", "#AAFFFFFF");
                await DoSmartSync(isAutoSync: true);
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().Location),
                Visible = true,
                Text = "Floating Reminder"
            };

            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("Show Dashboard", OnShowDashboard);
            contextMenu.MenuItems.Add("Close Dashboard", OnMinimizeToTray);
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Exit", OnExitApplication);

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.DoubleClick += OnShowDashboard;
        }

        private void SaveAndRefreshSettings()
        {
            if (double.TryParse(FontSizeInput.Text, out double fontSize))
            {
                _settings.StartFontSize = fontSize;
            }
            _settings.IsGlowEnabled = GlowCheckBox.IsChecked == true;

            SettingsService.SaveSettings(_settings, _currentUsername);
        }

        // --- DASHBOARD ACTIONS (UPDATED) ---

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) { ShowError("Message can't be empty."); return; }
            if (!int.TryParse(DurationInput.Text, out int duration) || duration <= 0) { ShowError("Duration must be a positive number."); return; }

            if (_editingItem != null)
            {
                var itemToUpdate = _settings.Items.FirstOrDefault(i => i.Id == _editingItem.Id);
                if (itemToUpdate != null)
                {
                    itemToUpdate.Message = message;
                    itemToUpdate.DurationSeconds = duration;
                    itemToUpdate.LastModified = DateTime.UtcNow; // Set timestamp
                }
            }
            else
            {
                var newItem = new ReminderItem { Message = message, DurationSeconds = duration };
                _settings.Items.Add(newItem);
            }

            SaveAndRefreshSettings();
            RefreshVisualList();

            MessageInput.Text = "";
            DurationInput.Text = "5";
            AddButton.Content = "ADD MESSAGE";
            ErrorText.Visibility = Visibility.Collapsed;
            _editingItem = null;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                var itemToMark = _settings.Items.FirstOrDefault(i => i.Id == item.Id);
                if (itemToMark != null)
                {
                    itemToMark.IsDeleted = true;
                    itemToMark.LastModified = DateTime.UtcNow; // Set timestamp
                }

                SaveAndRefreshSettings();
                RefreshVisualList();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                MessageInput.Text = item.Message;
                DurationInput.Text = item.DurationSeconds.ToString();
                AddButton.Content = "UPDATE MESSAGE";
                _editingItem = item;
                MessageInput.Focus();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (Reminders.Count == 0) { ShowError("Please add at least one message."); return; }

            SaveAndRefreshSettings();

            _noteWindow?.Close();
            _noteWindow = new FloatingNoteWindow(_settings);
            _noteWindow.Show();
            this.Hide();
        }

        // UPDATED: This is now the MANUAL sync button
        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SetSyncStatus("Syncing...", "#AAFFFFFF");
            // Run the sync logic, and show full errors if it fails
            await DoSmartSync(isAutoSync: false);
        }

        // This old button is no longer used
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            // Replaced by SyncButton_Click
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            this.Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            // Stop the timer when we log out
            _syncTimer?.Stop();

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // --- APP LIFECYCLE ---

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut)
            {
                e.Cancel = false;
                return;
            }
            if (!_isGuest)
            {
                e.Cancel = true;
                OnMinimizeToTray(null, EventArgs.Empty);
            }
        }

        private void OnShowDashboard(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        }

        private void OnMinimizeToTray(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            OnMinimizeToTray(null, EventArgs.Empty);
        }

        private void OnExitApplication(object sender, EventArgs e)
        {
            _notifyIcon?.Dispose();
            _noteWindow?.Close();
            Application.Current.Shutdown();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            OnExitApplication(null, EventArgs.Empty);
        }

        // --- Other Helpers ---

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void ChangeGradientButton_Click(object sender, RoutedEventArgs e)
        {
            _currentGradientIndex = (_currentGradientIndex + 1) % GradientPresets.SpotifyLikeGradients.Count;
            BackgroundGrid.Background = GradientPresets.SpotifyLikeGradients[_currentGradientIndex];
        }

        private void SetSyncStatus(string message, string color)
        {
            // Use Dispatcher to ensure UI update is on the main thread
            // This is safer when called from background timers
            Dispatcher.Invoke(() =>
            {
                SyncStatusText.Text = message;
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color);
                SyncStatusText.Visibility = Visibility.Visible;
            });
        }
    }
}