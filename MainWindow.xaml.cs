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

        public MainWindow()
        {
            InitializeComponent();

            _settings = SettingsService.LoadSettings();

            // Load and sort by timestamp
            Reminders = new ObservableCollection<ReminderItem>(_settings.Items.OrderBy(item => item.CreatedAt));
            RemindersList.ItemsSource = Reminders;
            FontSizeInput.Text = _settings.StartFontSize.ToString();
            GlowCheckBox.IsChecked = _settings.IsGlowEnabled;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            // REMOVED: this.Hide();
            // The window will now be visible on startup.
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
            contextMenu.MenuItems.Add("Close Dashboard", OnMinimizeToTray); // NEW
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Exit", OnExitApplication);

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.DoubleClick += OnShowDashboard;
        }

        // --- HELPER METHOD ---
        private void SaveAndRefreshSettings()
        {
            // Sort by creation time before saving
            _settings.Items = Reminders.OrderBy(item => item.CreatedAt).ToList();

            if (double.TryParse(FontSizeInput.Text, out double fontSize))
            {
                _settings.StartFontSize = fontSize;
            }
            _settings.IsGlowEnabled = GlowCheckBox.IsChecked == true;

            SettingsService.SaveSettings(_settings);
        }

        // --- DASHBOARD ACTIONS (UPDATED) ---

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) { ShowError("Message can't be empty."); return; }
            if (!int.TryParse(DurationInput.Text, out int duration) || duration <= 0) { ShowError("Duration must be a positive number."); return; }

            if (_editingItem != null)
            {
                // Update existing item
                _editingItem.Message = message;
                _editingItem.DurationSeconds = duration;
                // Note: We don't update the timestamp when editing
            }
            else
            {
                // Create a new item (ID and Timestamp are set in constructor)
                var newItem = new ReminderItem { Message = message, DurationSeconds = duration };
                Reminders.Add(newItem);
            }

            // Refresh the list to show the new item
            // We re-sort the underlying ObservableCollection
            var sortedList = Reminders.OrderBy(item => item.CreatedAt).ToList();
            Reminders.Clear();
            foreach (var item in sortedList)
            {
                Reminders.Add(item);
            }

            // Clear inputs
            MessageInput.Text = "";
            DurationInput.Text = "5";
            AddButton.Content = "ADD MESSAGE";
            ErrorText.Visibility = Visibility.Collapsed;
            _editingItem = null; // Ensure we are no longer in edit mode

            SaveAndRefreshSettings();
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

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                Reminders.Remove(item);
                SaveAndRefreshSettings();
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        // --- THEME & DRAGGING ---
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

        // --- LAUNCH BUTTON ---
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (Reminders.Count == 0) { ShowError("Please add at least one message."); return; }

            SaveAndRefreshSettings();

            _noteWindow?.Close();
            _noteWindow = new FloatingNoteWindow(_settings);
            _noteWindow.Show();
            this.Hide(); // Hide dashboard when note is launched
        }

        // --- SYNC (UPLOAD) BUTTON ---
        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SyncStatusText.Text = "Syncing (Uploading)...";
            SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#AAFFFFFF");
            SyncStatusText.Visibility = Visibility.Visible;
            SyncButton.IsEnabled = false;
            LoadButton.IsEnabled = false;

            try
            {
                SaveAndRefreshSettings(); // Saves and sorts the list
                await MongoSyncService.UploadSettingsAsync(_settings);

                SyncStatusText.Text = "Upload complete! ✨";
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#90EE90");
            }
            catch (Exception ex)
            {
                SyncStatusText.Text = "Sync Error. Check connection string or internet.";
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#FF6B6B");
                Console.WriteLine($"[SYNC ERROR]: {ex.Message}");
            }
            finally
            {
                SyncButton.IsEnabled = true;
                LoadButton.IsEnabled = true;
            }
        }

        // --- LOAD (DOWNLOAD & MERGE) BUTTON (UPDATED) ---
        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            SyncStatusText.Text = "Loading & Merging...";
            SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#AAFFFFFF");
            SyncStatusText.Visibility = Visibility.Visible;
            LoadButton.IsEnabled = false;
            SyncButton.IsEnabled = false;

            try
            {
                // 1. Get both local and cloud lists
                var cloudSettings = await MongoSyncService.DownloadSettingsAsync();
                var localItems = _settings.Items ?? new List<ReminderItem>();
                var cloudItems = cloudSettings?.Items ?? new List<ReminderItem>();

                if (cloudSettings == null)
                {
                    SyncStatusText.Text = "No settings found in cloud. Nothing to merge.";
                    SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#FF6B6B");
                    return;
                }

                // 2. Perform the merge
                // This combines both lists, groups them by their unique ID,
                // takes the first one from each group, and then sorts by time.
                var mergedList = localItems.Concat(cloudItems)
                                       .GroupBy(item => item.Id)
                                       .Select(group => group.First())
                                       .OrderBy(item => item.CreatedAt)
                                       .ToList();

                // 3. Update the main settings object
                _settings.Items = mergedList;
                // We keep local Font/Glow settings, they are not merged.

                // 4. Update the UI
                Reminders.Clear();
                foreach (var item in mergedList)
                {
                    Reminders.Add(item);
                }

                // 5. Save the new merged list locally
                SettingsService.SaveSettings(_settings);
                // We do NOT auto-upload. This lets the user verify.

                // 6. Report success
                SyncStatusText.Text = $"Merge complete! Total items: {mergedList.Count}";
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#90EE90");
            }
            catch (Exception ex)
            {
                SyncStatusText.Text = "Load Error. Check connection or internet.";
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#FF6B6B");
                Console.WriteLine($"[LOAD ERROR]: {ex.Message}");
            }
            finally
            {
                LoadButton.IsEnabled = true;
                SyncButton.IsEnabled = true;
            }
        }

        // --- APP LIFECYCLE (UPDATED) ---
        private void OnShowDashboard(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        }

        // NEW: Minimize to tray
        private void OnMinimizeToTray(object sender, EventArgs e)
        {
            this.Hide();
        }

        // NEW: Minimize button click
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Re-route the window's 'X' button to minimize instead of close
            e.Cancel = true;
            OnMinimizeToTray(null, EventArgs.Empty);
        }
    }
}