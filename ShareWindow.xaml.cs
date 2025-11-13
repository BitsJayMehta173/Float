using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace FloatingReminder
{
    public partial class ShareWindow : Window
    {
        private readonly ReminderCollection _collection;
        private readonly string _senderUsername;

        /// <summary>
        /// This property is used by MainWindow to know if the share was successful.
        /// </summary>
        public bool WasShareSuccessful { get; private set; } = false;

        public ShareWindow(ReminderCollection collection, List<string> friendUsernames, string senderUsername)
        {
            InitializeComponent();

            _collection = collection;
            _senderUsername = senderUsername;

            // Set the title
            CollectionNameText.Text = $"Collection: '{_collection.Title}'";

            // Populate the dropdown
            FriendsComboBox.ItemsSource = friendUsernames;
            if (friendUsernames.Count > 0)
            {
                FriendsComboBox.SelectedIndex = 0;
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (FriendsComboBox.SelectedItem == null)
            {
                ShowError("Please select a friend.");
                return;
            }

            string recipientUsername = FriendsComboBox.SelectedItem as string;

            SetLoading(true);

            try
            {
                // Call the service method we created
                await ShareService.SendCollectionAsync(_collection, _senderUsername, recipientUsername);

                // Set success flag and close
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
            SendButton.Content = isLoading ? "Sending..." : "Send";
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