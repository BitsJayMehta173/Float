using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Requires System.Windows.Forms reference
using System.Drawing;       // Requires System.Drawing reference
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button; // resolve ambiguity
using ContextMenu = System.Windows.Forms.ContextMenu; // FIX: Resolve ContextMenu ambiguity
using System.Windows.Media; // Needed for gradient brushes
using System.Windows.Input; // Added for MouseButtonEventArgs

namespace FloatingNote
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private FloatingNoteWindow _noteWindow;
        private int _currentGradientIndex = 0;

        public ObservableCollection<ReminderItem> Reminders { get; set; } = new ObservableCollection<ReminderItem>();
        private ReminderItem _editingItem = null;

        public MainWindow()
        {
            InitializeComponent();

            if (Reminders.Count == 0)
            {
                Reminders.Add(new ReminderItem { Message = "Welcome to your new dashboard! ✨", DurationSeconds = 5 });
                Reminders.Add(new ReminderItem { Message = "Add your own messages below 👇", DurationSeconds = 8 });
            }

            RemindersList.ItemsSource = Reminders;
            FontSizeInput.Text = "60";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            ApplyCurrentBackgroundPreset();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "Floating Note Dashboard"
            };

            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("Show Dashboard", OnShowDashboard);
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Exit", OnExitApplication);
            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.Click += OnShowDashboard;
        }

        // --- NEW WINDOW CONTROL HANDLERS ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allows dragging the window by the custom title bar
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Hides window to tray, same as the old standard close button
            this.Hide();
        }
        // -----------------------------------

        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            _currentGradientIndex = (_currentGradientIndex + 1) % GradientPresets.SpotifyLikeGradients.Count;
            ApplyCurrentBackgroundPreset();
        }

        private void ApplyCurrentBackgroundPreset()
        {
            if (this.Resources.Contains("DashboardBackgroundBrush"))
            {
                this.Resources["DashboardBackgroundBrush"] = GradientPresets.SpotifyLikeGradients[_currentGradientIndex];
            }
        }

        // --- CRUD OPERATIONS --- (Unchanged)

        private void AddUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = MsgInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) { ShowError("Please enter a message first."); return; }
            if (!int.TryParse(DurInput.Text, out int duration) || duration <= 0) { ShowError("Please enter a valid positive duration."); return; }

            if (_editingItem == null)
            {
                Reminders.Add(new ReminderItem { Message = msg, DurationSeconds = duration });
            }
            else
            {
                _editingItem.Message = msg;
                _editingItem.DurationSeconds = duration;
                int index = Reminders.IndexOf(_editingItem);
                if (index != -1) Reminders[index] = new ReminderItem { Message = msg, DurationSeconds = duration };
                ExitEditMode();
            }
            ClearInputs();
            ErrorText.Visibility = Visibility.Collapsed;
            if (_editingItem == null && Reminders.Count > 0) RemindersList.ScrollIntoView(Reminders[Reminders.Count - 1]);
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReminderItem item) EnterEditMode(item);
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReminderItem item)
            {
                Reminders.Remove(item);
                if (_editingItem == item) ExitEditMode();
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
            ClearInputs();
        }

        // --- HELPER METHODS --- (Unchanged)

        private void EnterEditMode(ReminderItem item)
        {
            _editingItem = item;
            MsgInput.Text = item.Message;
            DurInput.Text = item.DurationSeconds.ToString();
            InputHeader.Text = "Editing Message...";
            AddUpdateButton.Content = "UPDATE ✓";
            AddUpdateButton.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FF4CAF50");
            CancelEditButton.Visibility = Visibility.Visible;
            MsgInput.Focus();
        }

        private void ExitEditMode()
        {
            _editingItem = null;
            InputHeader.Text = "Add New Message";
            AddUpdateButton.Content = "ADD +";
            AddUpdateButton.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FF007ACC");
            CancelEditButton.Visibility = Visibility.Collapsed;
        }

        private void ClearInputs()
        {
            MsgInput.Text = "";
            DurInput.Text = "5";
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        // --- LAUNCH LOGIC --- (Unchanged)

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (Reminders.Count == 0) { ShowError("Please add at least one message."); return; }
            if (!double.TryParse(FontSizeInput.Text, out double fontSize) || fontSize <= 0) { ShowError("Invalid font size."); return; }

            var settings = new Settings { Items = Reminders.ToList(), StartFontSize = fontSize, IsGlowEnabled = GlowCheckBox.IsChecked == true };
            _noteWindow?.Close();
            _noteWindow = new FloatingNoteWindow(settings);
            _noteWindow.Show();
            this.Hide();
        }

        // --- APP LIFECYCLE --- (Unchanged)

        private void OnShowDashboard(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
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
            e.Cancel = true;
            this.Hide();
        }
    }
}