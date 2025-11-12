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
using System.Windows.Threading;

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

        private DispatcherTimer _syncTimer;

        public ObservableCollection<FriendRequest> PendingRequests { get; set; } = new ObservableCollection<FriendRequest>();

        public MainWindow(string username, bool isGuest)
        {
            InitializeComponent();

            _currentUsername = username;
            _isGuest = isGuest;

            _settings = SettingsService.LoadSettings(_currentUsername);

            RemindersList.ItemsSource = Reminders;
            RefreshVisualList();

            FriendRequestsList.ItemsSource = PendingRequests;

            FontSizeInput.Text = _settings.StartFontSize.ToString();
            GlowCheckBox.IsChecked = _settings.IsGlowEnabled;

            ConfigureUIMode();

            if (!_isGuest)
            {
                _syncTimer = new DispatcherTimer();
                _syncTimer.Interval = TimeSpan.FromMinutes(5);
                _syncTimer.Tick += OnAutoSyncTimer_Tick;
                _syncTimer.Start();
            }
        }

        private async void OnAutoSyncTimer_Tick(object sender, EventArgs e)
        {
            await DoSmartSync(isAutoSync: true);
        }

        private async Task DoSmartSync(bool isAutoSync = false)
        {
            if (_isGuest) return;
            if (SyncButton != null) SyncButton.IsEnabled = false;

            try
            {
                var localItems = _settings.Items ?? new List<ReminderItem>();
                var cloudSettings = await MongoSyncService.DownloadSettingsAsync(_currentUsername);
                var cloudItems = cloudSettings?.Items ?? new List<ReminderItem>();

                var allItems = localItems.Concat(cloudItems)
                                         .GroupBy(item => item.Id);

                var finalMergedList = allItems.Select(group =>
                {
                    return group.OrderByDescending(item => item.LastModified).First();
                })
                    .ToList();

                _settings.Items = finalMergedList;
                SaveAndRefreshSettings();
                await MongoSyncService.UploadSettingsAsync(_settings, _currentUsername);

                RefreshVisualList();

                SetSyncStatus($"✅ Synced ({DateTime.Now:h:mm tt})", "#90EE90");
            }
            catch (Exception ex)
            {
                string error = "Offline. (Last sync failed)";
                if (!isAutoSync) error = "Sync Error. Check connection.";

                SetSyncStatus(error, "#FF6B6B");
                Console.WriteLine($"[SYNC ERROR]: {ex.Message}");
            }
            finally
            {
                if (SyncButton != null) SyncButton.IsEnabled = true;
            }
        }

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
                FriendsTab.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = $"Dashboard ({_currentUsername})";
                LoginButton.Visibility = Visibility.Collapsed;
                LogoutButton.Visibility = Visibility.Visible;
                SyncButton.Visibility = Visibility.Visible;
                LoadButton.Visibility = Visibility.Collapsed;
                FriendsTab.Visibility = Visibility.Visible;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!_isGuest)
            {
                SetSyncStatus("Syncing...", "#AAFFFFFF");
                await DoSmartSync(isAutoSync: true);
                await LoadFriendRequestsAsync();
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

        // --- DASHBOARD ACTIONS ---

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
                    itemToUpdate.LastModified = DateTime.UtcNow;
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
                    itemToMark.LastModified = DateTime.UtcNow;
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

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SetSyncStatus("Syncing...", "#AAFFFFFF");
            await DoSmartSync(isAutoSync: false);
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e) { }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            _syncTimer?.Stop();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // --- APP LIFECYCLE ---

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut || _isGuest)
            {
                _notifyIcon?.Dispose();
                _noteWindow?.Close();
                e.Cancel = false;
            }
            else
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

        // --- Helper methods ---

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
            Dispatcher.Invoke(() =>
            {
                SyncStatusText.Text = message;
                SyncStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color);
                SyncStatusText.Visibility = Visibility.Visible;
            });
        }

        // --- FRIENDS TAB METHODS ---

        private void SetFriendStatus(string message, string color)
        {
            Dispatcher.Invoke(() =>
            {
                FriendStatusText.Text = message;
                FriendStatusText.Foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color);
                FriendStatusText.Visibility = Visibility.Visible;
            });
        }

        private async Task LoadFriendRequestsAsync()
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                return;
            }

            try
            {
                var requests = await FriendService.GetPendingRequestsAsync(_currentUsername);
                PendingRequests.Clear();
                foreach (var req in requests)
                {
                    PendingRequests.Add(req);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendLoad Error]: {ex.Message}");
            }
        }

        private async void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                SetFriendStatus("You must be online to send friend requests.", "#FF6B6B");
                return;
            }

            string recipient = SearchUsernameBox.Text.Trim();
            if (string.IsNullOrEmpty(recipient))
            {
                SetFriendStatus("Please enter a username.", "#FF6B6B");
                return;
            }

            SendRequestButton.IsEnabled = false;
            SetFriendStatus("Sending...", "#AAFFFFFF");

            try
            {
                string result = await FriendService.SendFriendRequestAsync(_currentUsername, recipient);

                if (result.StartsWith("Success"))
                {
                    SetFriendStatus(result, "#90EE90");
                    SearchUsernameBox.Text = "";
                }
                else
                {
                    SetFriendStatus(result, "#FF6B6B");
                }
            }
            catch (Exception ex)
            {
                SetFriendStatus("An error occurred. Please try again.", "#FF6B6B");
                Console.WriteLine($"[SendRequest Error]: {ex.Message}");
            }
            finally
            {
                SendRequestButton.IsEnabled = true;
            }
        }

        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("You must be online to accept requests.", "#FF6B6B");
                return;
            }

            if (sender is Button btn && btn.Tag is FriendRequest request)
            {
                try
                {
                    await FriendService.AcceptRequestAsync(request);
                    PendingRequests.Remove(request);
                }
                catch (Exception ex)
                {
                    SetSyncStatus("Error accepting request. Try again.", "#FF6B6B");
                    Console.WriteLine($"[AcceptRequest Error]: {ex.Message}");
                }
            }
        }

        private async void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("You must be online to decline requests.", "#FF6B6B");
                return;
            }

            if (sender is Button btn && btn.Tag is FriendRequest request)
            {
                try
                {
                    await FriendService.DeclineRequestAsync(request);
                    PendingRequests.Remove(request);
                }
                catch (Exception ex)
                {
                    SetSyncStatus("Error declining request. Try again.", "#FF6B6B");
                    Console.WriteLine($"[DeclineRequest Error]: {ex.Message}");
                }
            }
        }
    }
}