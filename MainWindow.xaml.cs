using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FloatingReminder
{
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private FloatingNoteWindow _noteWindow;
        private int _currentGradientIndex = 0;
        private bool _isLoggingOut = false;

        // --- UI-Bound Lists ---
        public ObservableCollection<ReminderItem> Reminders { get; set; } = new ObservableCollection<ReminderItem>();
        public ObservableCollection<ReminderCollection> Collections { get; set; } = new ObservableCollection<ReminderCollection>();
        public ObservableCollection<FriendRequest> PendingRequests { get; set; } = new ObservableCollection<FriendRequest>();

        private ReminderItem _editingItem = null;

        // --- Master Data ---
        private Settings _settings;
        private List<ReminderCollection> _masterCollections;
        private ReminderCollection _activeCollection;

        // --- Session Info ---
        private readonly string _username;
        private readonly bool _isGuest;
        private User _currentUser;

        public MainWindow(string username, bool isGuest)
        {
            _username = username;
            _isGuest = isGuest;
            DataContext = this;
            InitializeComponent();
            InitializeTrayIcon();

            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        #region Window and Tray Logic

        private void InitializeTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().Location),
                Visible = true,
                Text = "Floating Reminder"
            };

            var contextMenu = new System.Windows.Forms.ContextMenu();
            contextMenu.MenuItems.Add("Show Dashboard", (s, e) => ShowDashboard());
            contextMenu.MenuItems.Add("Exit", (s, e) => ExitApplication());
            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowDashboard();
        }

        private void ShowDashboard()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            _isLoggingOut = true;

            _noteWindow?.Close();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut)
            {
                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;

            _noteWindow?.Close();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            this.Close();
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void ChangeBgButton_Click(object sender, RoutedEventArgs e)
        {
            _currentGradientIndex = (_currentGradientIndex + 1) % GradientPresets.SpotifyLikeGradients.Count;
            this.Background = GradientPresets.SpotifyLikeGradients[_currentGradientIndex];
        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Background = GradientPresets.SpotifyLikeGradients[_currentGradientIndex];
            SetSyncStatus("Loading settings...", "#FFFFFF");

            _settings = SettingsService.LoadSettings(_username);
            _masterCollections = MongoSyncService.LoadCollectionsFromLocal(_username);
            LoadCollectionsFromMasterList();

            if (_isGuest)
            {
                TitleText.Text = $"Dashboard (Guest)";
                SetSyncStatus("Ready (Guest Mode)", "#FFFFFF");
                LogoutButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = $"Dashboard ({_username})";
                SetSyncStatus("Syncing...", "#FFD700");
                try
                {
                    _currentUser = await FriendService.GetUserAsync(_username);
                    if (_currentUser == null) throw new Exception("User not found in DB.");
                }
                catch (Exception ex)
                {
                    SetSyncStatus("Error loading user data. Read-only.", "#FF6B6B");
                    Console.WriteLine($"[LoadUser Error]: {ex.Message}");
                }

                await SyncWithCloud();

                // *** ADD THIS LINE ***
                await LoadPendingRequestsAsync();
            }

            SetActiveCollection(_settings.ActiveCollectionId);
            await CheckForSharedCollectionsAsync();
        }

        #endregion

        #region Data Sync and Management

        private async Task SyncWithCloud()
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("Offline mode. Local data loaded.", "#FFFFFF");
                return;
            }

            try
            {
                var cloudCollections = await MongoSyncService.LoadCollectionsFromCloudAsync(_username);

                _masterCollections = cloudCollections;
                MongoSyncService.SaveCollectionsToLocal(_masterCollections, _username);
                LoadCollectionsFromMasterList();
                SetSyncStatus("Sync complete.", "#90EE90");
            }
            catch (Exception ex)
            {
                SetSyncStatus("Sync failed. Using local data.", "#FF6B6B");
                Console.WriteLine($"[Sync Error]: {ex.Message}");
            }
        }

        private async Task SaveAndSyncMasterCollections(bool forceCloudSync = false)
        {
            MongoSyncService.SaveCollectionsToLocal(_masterCollections, _username);
            SetSyncStatus("Local data saved.", "#FFFFFF");

            if (forceCloudSync && !_isGuest && NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("Syncing to cloud...", "#FFD700");
                try
                {
                    await MongoSyncService.SaveCollectionsToCloudAsync(_masterCollections, _username);
                    SetSyncStatus("Cloud sync complete.", "#90EE90");
                }
                catch (Exception ex)
                {
                    SetSyncStatus("Cloud sync failed.", "#FF6B6B");
                    Console.WriteLine($"[CloudSave Error]: {ex.Message}");
                }
            }
        }

        private void SetActiveCollection(string collectionId)
        {
            if (collectionId == null)
            {
                _activeCollection = _masterCollections.FirstOrDefault();
            }
            else
            {
                _activeCollection = _masterCollections.FirstOrDefault(c => c.Id == collectionId);
            }

            if (_activeCollection == null)
            {
                _activeCollection = new ReminderCollection { Title = "My First Collection" };
                _masterCollections.Add(_activeCollection);
                Collections.Add(_activeCollection);
            }

            _settings.ActiveCollectionId = _activeCollection.Id;
            RefreshRemindersList(); // This just updates the ObservableCollection
        }

        private void StartFloatingWindow()
        {
            if (_noteWindow != null)
            {
                _noteWindow.Close();
            }
            _noteWindow = new FloatingNoteWindow(_activeCollection.Items, _settings);
            _noteWindow.Show();
        }

        private void SetSyncStatus(string message, string hexColor)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetSyncStatus(message, hexColor));
                return;
            }
            SyncStatusText.Text = message;
            SyncStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }

        #endregion

        #region Reminder Tab Logic

        private void BackToCollections_Click(object sender, RoutedEventArgs e)
        {
            if (_noteWindow != null)
            {
                _noteWindow.Close();
                _noteWindow = null;
            }

            MainTabControl.SelectedItem = CollectionsTab;
            RemindersTab.Visibility = Visibility.Collapsed;
        }

        // --- NEW: Event handler for the "Launch" button ---
        private void LaunchFloatWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCollection != null)
            {
                StartFloatingWindow();
            }
            else
            {
                SetSyncStatus("Error: No collection is active.", "#FF6B6B");
            }
        }

        private void RefreshRemindersList()
        {
            Reminders.Clear();
            if (_activeCollection != null)
            {
                var activeItems = _activeCollection.Items.Where(i => !i.IsDeleted);
                foreach (var item in activeItems)
                {
                    Reminders.Add(item);
                }
            }

            // This logic is key: only refresh the window if it's already open
            if (_noteWindow != null)
            {
                StartFloatingWindow();
            }
        }

        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewItemTextBox.Text) || NewItemTextBox.Text == "New reminder...")
            {
                return;
            }

            var newItem = new ReminderItem
            {
                Message = NewItemTextBox.Text,
                DurationSeconds = 5
            };

            _activeCollection.Items.Add(newItem);
            _activeCollection.LastModified = DateTime.UtcNow;
            Reminders.Add(newItem);

            NewItemTextBox.Text = "New reminder...";
            NewItemTextBox.Foreground = new SolidColorBrush(Colors.Gray);

            await SaveAndSyncMasterCollections(true);

            // --- MODIFIED ---
            RefreshRemindersList(); // Only refreshes if open
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                item.IsDeleted = true;
                item.LastModified = DateTime.UtcNow;
                _activeCollection.LastModified = DateTime.UtcNow;

                Reminders.Remove(item);
                await SaveAndSyncMasterCollections(true);

                // --- MODIFIED ---
                RefreshRemindersList(); // Only refreshes if open
            }
        }

        #endregion

        #region Collection Tab Logic

        private void LoadCollectionsFromMasterList()
        {
            Collections.Clear();
            foreach (var col in _masterCollections.Where(c => !c.IsDeleted).OrderBy(c => c.Title))
            {
                Collections.Add(col);
            }
        }

        // --- MODIFIED ---
        private void OpenCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                SetActiveCollection(collection.Id);
                SettingsService.SaveSettings(_settings, _username);
                RemindersTab.Visibility = Visibility.Visible;
                MainTabControl.SelectedItem = RemindersTab;

                // --- REMOVED ---
                // StartFloatingWindow(); // This is no longer called here
            }
        }

        private async void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                collection.IsDeleted = true;
                collection.LastModified = DateTime.UtcNow;
                Collections.Remove(collection);

                await SaveAndSyncMasterCollections(true);

                if (_activeCollection.Id == collection.Id)
                {
                    _noteWindow?.Close();
                    _noteWindow = null;
                    SetActiveCollection(null);
                }
            }
        }

        private async void AddNewCollection_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewColTextBox.Text) || NewColTextBox.Text == "New collection title...")
            {
                return;
            }

            var newCollection = new ReminderCollection
            {
                Title = NewColTextBox.Text
            };

            _masterCollections.Add(newCollection);
            Collections.Add(newCollection);

            await SaveAndSyncMasterCollections(true);

            NewColTextBox.Text = "New collection title...";
            NewColTextBox.Foreground = new SolidColorBrush(Colors.Gray);
        }

        private void CollectionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Does nothing
        }

        #endregion

        #region Share Collection Logic

        private void ShareCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                if (_isGuest)
                {
                    SetSyncStatus("You must be logged in to share.", "#FF6B6B");
                    return;
                }
                if (_currentUser == null || _currentUser.FriendUsernames == null || _currentUser.FriendUsernames.Count == 0)
                {
                    SetSyncStatus("You must have friends to share.", "#FF6B6B");
                    return;
                }

                var shareWindow = new ShareWindow(collection, _currentUser.FriendUsernames, _username);
                shareWindow.Owner = this;
                shareWindow.ShowDialog();

                if (shareWindow.WasShareSuccessful)
                {
                    SetSyncStatus($"Sent '{collection.Title}'!", "#90EE90");
                }
            }
        }

        private async Task CheckForSharedCollectionsAsync()
        {
            if (_isGuest || !NetworkService.IsNetworkAvailable()) return;

            SetSyncStatus("Checking for new shares...", "#FFFFFF");
            List<SharedCollectionItem> newShares;
            try
            {
                newShares = await ShareService.GetNewSharesAsync(_username);
            }
            catch (Exception ex)
            {
                SetSyncStatus("Error checking for shares.", "#FF6B6B");
                Console.WriteLine($"[ShareCheck Error]: {ex.Message}");
                return;
            }

            if (newShares.Count == 0)
            {
                SetSyncStatus("Ready", "#90EE90");
                return;
            }

            SetSyncStatus($"Downloading {newShares.Count} new collection(s)...", "#FFD700");

            foreach (var share in newShares)
            {
                var newCollection = share.SharedCollectionData;
                newCollection.Id = Guid.NewGuid().ToString();
                newCollection.Title = $"{newCollection.Title} (from {share.SenderUsername})";
                newCollection.LastModified = DateTime.UtcNow;
                newCollection.CreatedAt = DateTime.UtcNow;

                foreach (var item in newCollection.Items)
                {
                    item.Id = Guid.NewGuid().ToString();
                    item.LastModified = newCollection.LastModified;
                    item.CreatedAt = newCollection.LastModified;
                }

                _masterCollections.Add(newCollection);
                Collections.Add(newCollection);

                await ShareService.DeleteShareAsync(share.Id);
            }

            await SaveAndSyncMasterCollections(true);
            SetSyncStatus($"Successfully downloaded {newShares.Count} new share(s)!", "#90EE90");
        }

        #endregion

        #region Friends Tab Logic

        // *** NEW METHOD ***
        private async Task LoadPendingRequestsAsync()
        {
            if (_isGuest || !NetworkService.IsNetworkAvailable()) return;

            SetSyncStatus("Loading friend requests...", "#FFFFFF");
            try
            {
                // 1. Get requests from the service
                var requests = await FriendService.GetPendingRequestsAsync(_username);

                // 2. Clear the UI list
                PendingRequests.Clear();

                // 3. Re-populate the UI list
                foreach (var req in requests)
                {
                    PendingRequests.Add(req);
                }

                // 4. Update the tab header with the count
                PendingRequestsTab.Header = $"Pending Requests ({PendingRequests.Count})";
            }
            catch (Exception ex)
            {
                SetSyncStatus("Error loading requests.", "#FF6B6B");
                Console.WriteLine($"[LoadRequests Error]: {ex.Message}");
            }
        }
        private async void AddFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FriendUsernameTextBox.Text)) return;
            if (!NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("You must be online to send requests.", "#FF6B6B");
                return;
            }

            SetSyncStatus("Sending request...", "#FFD700");
            string result = await FriendService.SendRequestAsync(_username, FriendUsernameTextBox.Text);

            if (result == "Success: Friend request sent!")
            {
                SetSyncStatus(result, "#90EE90");
                FriendUsernameTextBox.Text = "";
            }
            else
            {
                SetSyncStatus(result, "#FF6B6B");
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
                    PendingRequestsTab.Header = $"Pending Requests ({PendingRequests.Count})";
                    if (_currentUser != null && !_currentUser.FriendUsernames.Contains(request.SenderUsername))
                    {
                        _currentUser.FriendUsernames.Add(request.SenderUsername);
                    }
                    SetSyncStatus($"You are now friends with {request.SenderUsername}!", "#90EE90");
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
                    PendingRequestsTab.Header = $"Pending Requests ({PendingRequests.Count})";
                    SetSyncStatus("Request declined.", "#FFFFFF");
                }
                catch (Exception ex)
                {
                    SetSyncStatus("Error declining request. Try again.", "#FF6B6B");
                    Console.WriteLine($"[DeclineRequest Error]: {ex.Message}");
                }
            }
        }
        #endregion

        #region TextBox Placeholder Logic

        private void NewItemTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb.Text == "New reminder..." || tb.Text == "New collection title...")
                {
                    tb.Text = "";
                    tb.Foreground = new SolidColorBrush(Colors.White);
                }
            }
        }

        private void NewItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Foreground = new SolidColorBrush(Colors.Gray);
                    if (tb.Name == "NewItemTextBox")
                        tb.Text = "New reminder...";
                    else if (tb.Name == "NewColTextBox")
                        tb.Text = "New collection title...";
                }
            }
        }

        #endregion
    }
}