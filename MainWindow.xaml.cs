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

        // --- UI-Bound Lists ---
        public ObservableCollection<ReminderItem> Reminders { get; set; } = new ObservableCollection<ReminderItem>();
        public ObservableCollection<ReminderCollection> Collections { get; set; } = new ObservableCollection<ReminderCollection>();
        public ObservableCollection<FriendRequest> PendingRequests { get; set; } = new ObservableCollection<FriendRequest>();

        private ReminderItem _editingItem = null;

        // --- Master Data ---
        private Settings _settings; // Holds preferences (font, etc.) and ActiveCollectionId
        private List<ReminderCollection> _masterCollections; // The "real" list of all collections
        private ReminderCollection _activeCollection; // The collection currently being edited in the "Messages" tab

        private readonly string _currentUsername;
        private readonly bool _isGuest;
        private bool _isLoggingOut = false;
        private DispatcherTimer _syncTimer;

        public MainWindow(string username, bool isGuest)
        {
            InitializeComponent();

            _currentUsername = username;
            _isGuest = isGuest;

            // Bind the ListViews to their ObservableCollections
            RemindersList.ItemsSource = Reminders;
            CollectionsList.ItemsSource = Collections;
            FriendRequestsList.ItemsSource = PendingRequests;

            if (!_isGuest)
            {
                _syncTimer = new DispatcherTimer();
                _syncTimer.Interval = TimeSpan.FromMinutes(5);
                _syncTimer.Tick += OnAutoSyncTimer_Tick;
                _syncTimer.Start();
            }
        }

        // --- SYNC LOGIC ---

        private async void OnAutoSyncTimer_Tick(object sender, EventArgs e)
        {
            await DoSmartSync(isAutoSync: true);
        }

        private List<T> MergeLists<T>(List<T> local, List<T> cloud) where T : class
        {
            // This generic helper can merge ReminderItems OR ReminderCollections
            var allItems = local.Concat(cloud).GroupBy(item => (item as dynamic).Id);

            var finalList = allItems.Select(group =>
            {
                // Find the item with the latest 'LastModified' date
                return group.OrderByDescending(item => (item as dynamic).LastModified).First();
            })
                .ToList();

            return finalList;
        }

        private async Task DoSmartSync(bool isAutoSync = false)
        {
            if (_isGuest) return;
            if (SyncButton != null) SyncButton.IsEnabled = false;

            try
            {
                var localSettings = _settings;
                var cloudSettings = await MongoSyncService.DownloadSettingsAsync(_currentUsername) ?? new Settings();

                var localCollections = _masterCollections;
                var cloudCollections = await MongoSyncService.LoadCollectionsFromCloudAsync(_currentUsername);

                // Merge the user's preferences
                var finalSettings = (cloudSettings.Id == null) ? localSettings : cloudSettings;

                // Merge the collections
                var finalCollectionsList = MergeLists(localCollections, cloudCollections);

                _settings = finalSettings;
                _masterCollections = finalCollectionsList;

                // Save both master lists locally
                SettingsService.SaveSettings(_settings, _currentUsername);
                SettingsService.SaveCollections(_masterCollections, _currentUsername);

                // Save both master lists to the cloud
                await MongoSyncService.UploadSettingsAsync(_settings, _currentUsername);
                await MongoSyncService.SaveCollectionsToCloudAsync(_masterCollections, _currentUsername);

                // Refresh the UI from the new master lists
                RefreshCollectionsList();
                LoadActiveCollection(_settings.ActiveCollectionId); // This reloads the "Messages" tab

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

        // This is a specific helper for ReminderItems
        private List<ReminderItem> MergeItems(List<ReminderItem> local, List<ReminderItem> cloud)
        {
            var allItems = local.Concat(cloud).GroupBy(item => item.Id);
            var finalList = allItems.Select(group =>
            {
                return group.OrderByDescending(item => item.LastModified).First();
            })
                .ToList();
            return finalList;
        }

        // --- UI REFRESH HELPERS ---

        private void RefreshVisualList()
        {
            Reminders.Clear();
            if (_activeCollection == null)
            {
                // No collection is open. Disable the UI.
                MessagesTab.IsEnabled = false;
                return;
            }

            MessagesTab.IsEnabled = true;
            var activeItems = _activeCollection.Items
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.CreatedAt)
                .ToList();

            foreach (var item in activeItems)
            {
                Reminders.Add(item);
            }
        }

        private void RefreshCollectionsList()
        {
            var activeCollections = _masterCollections
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Title)
                .ToList();

            Collections.Clear();
            foreach (var col in activeCollections)
            {
                Collections.Add(col);
            }

            NoCollectionsText.Visibility = activeCollections.Any() ? Visibility.Collapsed : Visibility.Visible;
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

        // --- NEW: Helper to load a collection into the editor ---
        private void LoadActiveCollection(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId))
            {
                _activeCollection = null;
            }
            else
            {
                _activeCollection = _masterCollections.FirstOrDefault(c => c.Id == collectionId && !c.IsDeleted);
            }

            if (_activeCollection == null)
            {
                // The active collection was deleted or not found
                // Create a new "default" list for the user
                _activeCollection = new ReminderCollection { Title = "My First List" };
                _masterCollections.Add(_activeCollection);
                SaveCollections();
            }

            // Save this as the new active ID
            _settings.ActiveCollectionId = _activeCollection?.Id;
            SaveAppSettings();
            RefreshVisualList();

            // Update the header subtitle
            if (HeaderSubtitle != null)
            {
                HeaderSubtitle.Text = $"Editing: {_activeCollection.Title}";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load local data first
            _settings = SettingsService.LoadSettings(_currentUsername);
            _masterCollections = SettingsService.LoadCollections(_currentUsername);

            RefreshCollectionsList();
            LoadActiveCollection(_settings.ActiveCollectionId);

            FontSizeInput.Text = _settings.StartFontSize.ToString();
            GlowCheckBox.IsChecked = _settings.IsGlowEnabled;

            if (!_isGuest)
            {
                // Now sync with the cloud
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

        private void SaveAppSettings()
        {
            if (double.TryParse(FontSizeInput.Text, out double fontSize))
            {
                _settings.StartFontSize = fontSize;
            }
            _settings.IsGlowEnabled = GlowCheckBox.IsChecked == true;

            _settings.ActiveCollectionId = _activeCollection?.Id;

            SettingsService.SaveSettings(_settings, _currentUsername);
        }

        // Saves just the collection data
        private void SaveCollections()
        {
            SettingsService.SaveCollections(_masterCollections, _currentUsername);
        }

        // --- MESSAGES TAB ACTIONS ---

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCollection == null)
            {
                ShowError("Please open or create a collection first.");
                return;
            }

            string message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) { ShowError("Message can't be empty."); return; }
            if (!int.TryParse(DurationInput.Text, out int duration) || duration <= 0) { ShowError("Duration must be a positive number."); return; }

            if (_editingItem != null)
            {
                var itemToUpdate = _activeCollection.Items.FirstOrDefault(i => i.Id == _editingItem.Id);
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
                _activeCollection.Items.Add(newItem);
            }

            _activeCollection.LastModified = DateTime.UtcNow; // Mark collection as modified
            SaveCollections(); // Save master collections list to local file
            RefreshVisualList();

            MessageInput.Text = "";
            DurationInput.Text = "5";
            AddButton.Content = "ADD MESSAGE";
            ErrorText.Visibility = Visibility.Collapsed;
            _editingItem = null;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCollection == null) return;
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                var itemToMark = _activeCollection.Items.FirstOrDefault(i => i.Id == item.Id);
                if (itemToMark != null)
                {
                    itemToMark.IsDeleted = true;
                    itemToMark.LastModified = DateTime.UtcNow;
                }

                _activeCollection.LastModified = DateTime.UtcNow; // Mark collection as modified
                SaveCollections();
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
            if (_activeCollection == null)
            {
                ShowError("Please open a collection to launch.");
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            if (Reminders.Count == 0) { ShowError("Please add at least one message."); return; }

            SaveAppSettings(); // Save latest font/glow settings

            _noteWindow?.Close();
            // Pass the active collection's items and the app settings
            _noteWindow = new FloatingNoteWindow(_activeCollection.Items, _settings);
            _noteWindow.Show();
            this.Hide();
        }

        // --- COLLECTIONS TAB ACTIONS ---

        private void CreateCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            string title = NewColTitleBox.Text.Trim();
            if (string.IsNullOrEmpty(title) || title == "New Collection Title")
            {
                SetFriendStatus("Please enter a valid title.", "#FF6B6B"); // Use FriendStatus for this tab
                return;
            }

            var newCollection = new ReminderCollection
            {
                Title = title,
                LastModified = DateTime.UtcNow
            };

            _masterCollections.Add(newCollection);

            SaveCollections();
            RefreshCollectionsList();

            // Auto-open the new collection
            LoadActiveCollection(newCollection.Id);
            MainTabControl.SelectedItem = MessagesTab;

            NewColTitleBox.Text = "New Collection Title";
            SetFriendStatus("", "#FF6B6B"); // Clear error
        }

        private void Collection_OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                LoadActiveCollection(collection.Id);
                MainTabControl.SelectedItem = MessagesTab;
            }
        }

        private async void Collection_DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                var collectionToMark = _masterCollections.FirstOrDefault(c => c.Id == collection.Id);

                if (collectionToMark != null)
                {
                    collectionToMark.IsDeleted = true;
                    collectionToMark.LastModified = DateTime.UtcNow;

                    // If we deleted the one we're editing, clear the editor
                    if (_activeCollection?.Id == collectionToMark.Id)
                    {
                        LoadActiveCollection(null);
                    }

                    SaveCollections();
                    RefreshCollectionsList();

                    if (!_isGuest)
                    {
                        SetSyncStatus("Syncing deletion...", "#AAFFFFFF");
                        await DoSmartSync(isAutoSync: true);
                    }
                }
            }
        }

        // --- TAB CONTROL HELPER ---
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HeaderSubtitle == null) return;

            if (MainTabControl.SelectedItem == MessagesTab)
            {
                HeaderSubtitle.Text = _activeCollection != null ? $"Editing: {_activeCollection.Title}" : "No Collection Open";
            }
            else if (MainTabControl.SelectedItem == CollectionsTab)
            {
                HeaderSubtitle.Text = "Your saved message collections.";
            }
            else if (MainTabControl.SelectedItem == FriendsTab)
            {
                HeaderSubtitle.Text = "Manage your friends and requests.";
            }
        }

        #region Unchanged Methods

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
        #endregion
    }
}