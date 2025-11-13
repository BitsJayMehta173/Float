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

            // Stop the real-time sync
            AppState.CleanupSession();

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

            // Stop the real-time sync
            AppState.CleanupSession();

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
                await LoadPendingRequestsAsync(); // Load friend requests

                // Start the real-time sync session
                AppState.InitializeSession(_username, OnCloudUpdate);
            }

            SetActiveCollection(_settings.ActiveCollectionId);
        }

        #endregion

        #region Data Sync and Management

        // --- NEW: This is the callback for the SyncManager ---
        private void OnCloudUpdate()
        {
            // This is called from a background thread,
            // so we MUST use the Dispatcher to update the UI.
            Dispatcher.Invoke(async () =>
            {
                SetSyncStatus("Change detected, re-syncing...", "#FFD700");

                // 1. Re-run the main cloud sync
                await SyncWithCloud();

                // 2. Re-set the active collection.
                // This re-loads the item list in the Reminders tab
                // in case the active collection was the one modified.
                SetActiveCollection(_settings.ActiveCollectionId);

                // 3. (Optional) Re-load friend requests
                await LoadPendingRequestsAsync();

                SetSyncStatus("Sync complete.", "#90EE90");
            });
        }

        private async Task SyncWithCloud()
        {
            if (!NetworkService.IsNetworkAvailable())
            {
                SetSyncStatus("Offline mode. Local data loaded.", "#FFFFFF");
                return;
            }

            try
            {
                // This service call now gets collections WE OWN or ARE SHARED WITH US
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

        private void SaveMasterCollectionsToLocal()
        {
            MongoSyncService.SaveCollectionsToLocal(_masterCollections, _username);
            SetSyncStatus("Local data saved.", "#FFFFFF");
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
                // Create a new collection only if the master list is empty
                if (!_masterCollections.Any())
                {
                    _activeCollection = new ReminderCollection
                    {
                        Title = "My First Collection",
                        OwnerUsername = _username // Set owner
                    };
                    _activeCollection.IsOwnedByCurrentUser = true;
                    _masterCollections.Add(_activeCollection);
                    Collections.Add(_activeCollection);
                }
                else
                {
                    _activeCollection = _masterCollections.First();
                }
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

            SaveMasterCollectionsToLocal();
            await MongoSyncService.SaveCollectionToCloudAsync(_activeCollection, _username);

            RefreshRemindersList();
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderItem item)
            {
                item.IsDeleted = true; // Soft delete
                item.LastModified = DateTime.UtcNow;
                _activeCollection.LastModified = DateTime.UtcNow;

                Reminders.Remove(item);

                SaveMasterCollectionsToLocal();
                await MongoSyncService.SaveCollectionToCloudAsync(_activeCollection, _username);

                RefreshRemindersList();
            }
        }

        #endregion

        #region Collection Tab Logic

        private void LoadCollectionsFromMasterList()
        {
            Collections.Clear();
            foreach (var col in _masterCollections.Where(c => !c.IsDeleted).OrderBy(c => c.Title))
            {
                // Set the IsOwned property for UI bindings
                col.IsOwnedByCurrentUser = (col.OwnerUsername == _username);
                Collections.Add(col);
            }
        }

        private void OpenCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                SetActiveCollection(collection.Id);
                SettingsService.SaveSettings(_settings, _username);
                RemindersTab.Visibility = Visibility.Visible;
                MainTabControl.SelectedItem = RemindersTab;
            }
        }

        private async void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collection)
            {
                // UI button should be disabled, but double-check
                if (!collection.IsOwnedByCurrentUser) return;

                collection.IsDeleted = true; // Soft delete
                collection.LastModified = DateTime.UtcNow;
                Collections.Remove(collection);

                SaveMasterCollectionsToLocal();
                await MongoSyncService.SaveCollectionToCloudAsync(collection, _username);

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
                Title = NewColTextBox.Text,
                OwnerUsername = _username
            };
            newCollection.IsOwnedByCurrentUser = true; // Set for UI

            _masterCollections.Add(newCollection);
            Collections.Add(newCollection);

            SaveMasterCollectionsToLocal();
            await MongoSyncService.SaveCollectionToCloudAsync(newCollection, _username);

            NewColTextBox.Text = "New collection title...";
            NewColTextBox.Foreground = new SolidColorBrush(Colors.Gray);
        }

        private async void CopyCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReminderCollection collectionToCopy)
            {
                // 1. Create the new collection
                var newCollection = new ReminderCollection
                {
                    Title = collectionToCopy.Title + " (Copy)",
                    OwnerUsername = _username, // You own the copy
                    IsOwnedByCurrentUser = true,
                    SharedWithUsernames = new List<string>() // The copy is private
                };

                // 2. Deep-copy all items
                foreach (var item in collectionToCopy.Items.Where(i => !i.IsDeleted))
                {
                    newCollection.Items.Add(new ReminderItem
                    {
                        Message = item.Message,
                        DurationSeconds = item.DurationSeconds,
                    });
                }

                // 3. Add to lists
                _masterCollections.Add(newCollection);
                Collections.Add(newCollection);

                // 4. Save
                SaveMasterCollectionsToLocal();
                await MongoSyncService.SaveCollectionToCloudAsync(newCollection, _username);
                SetSyncStatus($"Created copy of '{collectionToCopy.Title}'", "#90EE90");
            }
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
                    SetSyncStatus("You must have friends to invite.", "#FF6B6B");
                    return;
                }

                var shareWindow = new ShareWindow(collection, _currentUser.FriendUsernames, _username);
                shareWindow.Owner = this;
                shareWindow.ShowDialog();

                if (shareWindow.WasShareSuccessful)
                {
                    SetSyncStatus($"Invite list updated for '{collection.Title}'!", "#90EE90");
                }
            }
        }

        #endregion

        #region Friends Tab Logic

        private async Task LoadPendingRequestsAsync()
        {
            if (_isGuest || !NetworkService.IsNetworkAvailable()) return;

            SetSyncStatus("Loading friend requests...", "#FFFFFF");
            try
            {
                var requests = await FriendService.GetPendingRequestsAsync(_username);
                PendingRequests.Clear();
                foreach (var req in requests)
                {
                    PendingRequests.Add(req);
                }
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