using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FloatingReminder
{
    public partial class LoginWindow : Window
    {
        // NEW: Constructor that takes a success message
        public LoginWindow(string notificationMessage = null)
        {
            InitializeComponent();

            // If a notification was passed (e.g., from sign up)
            if (!string.IsNullOrEmpty(notificationMessage))
            {
                ShowSuccess(notificationMessage);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            SetLoading(true);
            string result = await AuthService.ValidateUserAsync(UsernameBox.Text, PasswordBox.Password);

            if (result == "Success")
            {
                OpenDashboard(UsernameBox.Text, false); // false = not a guest
            }
            else
            {
                // This is where "Invalid username or password" will be shown
                ShowError(result);
                SetLoading(false);
            }
        }

        // UPDATED: This button now navigates to the SignUpWindow
        private void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var signUpWindow = new SignUpWindow();
            signUpWindow.Show();
            this.Close();
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            OpenDashboard(SettingsService.GuestUsername, true); // true = is a guest
        }

        private void OpenDashboard(string username, bool isGuest)
        {
            var dashboard = new MainWindow(username, isGuest);
            dashboard.Show();
            this.Close();
        }

        // --- Helper Methods ---

        private void SetLoading(bool isLoading)
        {
            LoginButton.IsEnabled = !isLoading;
            CreateAccountButton.IsEnabled = !isLoading;
            GuestButton.IsEnabled = !isLoading;
            ErrorText.Visibility = Visibility.Collapsed;
            SuccessText.Visibility = Visibility.Collapsed;
            LoginButton.Content = isLoading ? "Loading..." : "Login";
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            SuccessText.Visibility = Visibility.Collapsed;
        }

        // NEW: Helper to show success notification
        private void ShowSuccess(string message)
        {
            SuccessText.Text = message;
            SuccessText.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}