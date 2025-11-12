using System;
using System.Windows;
using System.Windows.Input;

namespace FloatingReminder
{
    public partial class SignUpWindow : Window
    {
        public SignUpWindow()
        {
            InitializeComponent();
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            // --- Validation ---
            if (UsernameBox.Text.Length < 3)
            {
                ShowError("Username must be at least 3 characters.");
                return;
            }
            if (PasswordBox.Password.Length < 6)
            {
                ShowError("Password must be at least 6 characters.");
                return;
            }

            SetLoading(true);
            string result = await AuthService.RegisterUserAsync(UsernameBox.Text, PasswordBox.Password);

            if (result == "Success")
            {
                // Registration successful, go back to Login screen
                // We pass a success message to the LoginWindow constructor
                var loginWindow = new LoginWindow("Sign up successful! Please log in.");
                loginWindow.Show();
                this.Close();
            }
            else
            {
                // Show the error (e.g., "Username already exists")
                ShowError(result);
                SetLoading(false);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Go back to login
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the whole app
            Application.Current.Shutdown();
        }

        // --- Helper Methods ---

        private void SetLoading(bool isLoading)
        {
            SignUpButton.IsEnabled = !isLoading;
            BackButton.IsEnabled = !isLoading;
            ErrorText.Visibility = Visibility.Collapsed;
            SignUpButton.Content = isLoading ? "Creating..." : "Create Account";
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
    }
}