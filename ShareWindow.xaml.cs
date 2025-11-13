using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FloatingReminder
{
    public partial class ShareWindow : Window
    {
        private readonly ReminderCollection _collection;
        private readonly string _senderUsername;

        // --- NEW: This holds the list of FriendInviteViewModel objects ---
        private readonly ObservableCollection<FriendInviteViewModel> _friendInviteList;

        public bool WasShareSuccessful { get; private set; } = false;

        // --- MODIFIED: Constructor now takes the collection ---
        public ShareWindow(ReminderCollection collection, List<string> allFriendUsernames, string senderUsername)
        {
            InitializeComponent();

            _collection = collection;
            _senderUsername = senderUsername;
            _friendInviteList = new ObservableCollection<FriendInviteViewModel>();

            CollectionNameText.Text = $"Collection: '{_collection.Title}'";

            // --- NEW: Populate the checklist ---
            foreach (var friend in allFriendUsernames)
            {
                bool isAlreadyInvited = _collection.SharedWithUsernames.Contains(friend);

                _friendInviteList.Add(new FriendInviteViewModel
                {
                    Username = friend,
                    IsInvited = isAlreadyInvited,
                    CanInvite = !isAlreadyInvited // Disable checkbox if already invited
                });
            }

            FriendsListBox.ItemsSource = _friendInviteList;
        }

        // --- MODIFIED: This is now "Update Invites" logic ---
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            // Get all usernames that are NOW checked but WEREN'T before
            var newInvites = _friendInviteList
                .Where(f => f.IsInvited && f.CanInvite) // Find newly checked users
                .Select(f => f.Username)
                .ToList();

            if (!newInvites.Any())
            {
                // No new people were invited, just close
                this.Close();
                return;
            }

            SetLoading(true);

            try
            {
                // --- NEW: Add new friends to the list and save ---
                _collection.SharedWithUsernames.AddRange(newInvites);
                _collection.LastModified = DateTime.UtcNow;

                // Call the service method we created in MongoSyncService
                await MongoSyncService.SaveCollectionToCloudAsync(_collection, _senderUsername);

                WasShareSuccessful = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}");
                SetLoading(false);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #region Helper Methods

        private void SetLoading(bool isLoading)
        {
            SendButton.IsEnabled = !isLoading;
            CancelButton.IsEnabled = !isLoading;
            ErrorText.Visibility = Visibility.Collapsed;
            SendButton.Content = isLoading ? "Saving..." : "Update Invites";
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

        #endregion
    }
}