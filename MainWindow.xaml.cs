using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FloatingNote
{
    public partial class MainWindow : Window
    {
        private bool _isDragging;
        private Point _dragStart;
        private readonly DispatcherTimer _textChangeTimer;
        private int _currentTextIndex;
        private readonly string[] _texts;

        // Reuse fade animations instead of recreating every time
        private readonly DoubleAnimation _fadeOutAnim;
        private readonly DoubleAnimation _fadeInAnim;

        public MainWindow()
        {
            InitializeComponent();

            // Static text array (stored in readonly memory)
            _texts = new[]
            {
                "Breathe in peace 🌿",
                "You are doing great 💪",
                "Take a short break ☕",
                "Keep your focus sharp 🎯",
                "Progress, not perfection 🌱"
            };

            // Prebuild animations — these are reused every time
            _fadeOutAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            _fadeOutAnim.Completed += FadeOutAnim_Completed;

            _fadeInAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            UpdateCloseButtonSize();

            // Timer runs every 3 seconds
            _textChangeTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _textChangeTimer.Tick += TextChangeTimer_Tick;
            _textChangeTimer.Start();
        }

        private void TextChangeTimer_Tick(object sender, EventArgs e)
        {
            // increment index and wrap around
            int nextIndex = _currentTextIndex + 1;
            if (nextIndex >= _texts.Length) nextIndex = 0;

            // only change text if it’s actually different
            if (_texts[nextIndex] != ReminderText.Text)
            {
                _currentTextIndex = nextIndex;
                ReminderText.BeginAnimation(OpacityProperty, _fadeOutAnim);
            }
        }

        private void FadeOutAnim_Completed(object sender, EventArgs e)
        {
            // Update text and fade back in
            ReminderText.Text = _texts[_currentTextIndex];
            ReminderText.BeginAnimation(OpacityProperty, _fadeInAnim);
        }

        private void UpdateCloseButtonSize()
        {
            double ratio = 0.3;
            double btnSize = Math.Max(18, ReminderText.FontSize * ratio);
            CloseButton.Width = btnSize;
            CloseButton.Height = btnSize;
            CloseButton.FontSize = Math.Max(10, btnSize * 0.5);
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e) => FadeShowCloseButton();
        private void Grid_MouseLeave(object sender, MouseEventArgs e) => FadeHideCloseButton();

        private void FadeShowCloseButton()
        {
            CloseButton.Visibility = Visibility.Visible;
            var anime = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120));
            CloseButton.BeginAnimation(OpacityProperty, anime);
        }

        private void FadeHideCloseButton()
        {
            var anime = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
            anime.Completed += (s, e) => CloseButton.Visibility = Visibility.Collapsed;
            CloseButton.BeginAnimation(OpacityProperty, anime);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _textChangeTimer.Stop();
            Close();
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            const double step = 0.7; // small change
            double delta = e.Delta > 0 ? step : -step;
            double newSize = Math.Max(10, Math.Min(200, ReminderText.FontSize + delta));
            AnimateFontSizeChange(newSize);
        }

        private void AnimateFontSizeChange(double targetSize)
        {
            double from = ReminderText.FontSize;
            if (Math.Abs(targetSize - from) < 0.2) return; // skip tiny changes

            var anim = new DoubleAnimation(from, targetSize, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (s, e) =>
            {
                ReminderText.FontSize = targetSize;
                UpdateCloseButtonSize();
            };
            ReminderText.BeginAnimation(System.Windows.Controls.TextBlock.FontSizeProperty, anim);
        }

        private void TextArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            TextArea.CaptureMouse();
        }

        private void TextArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point current = e.GetPosition(this);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;

            var tt = TextArea.RenderTransform as System.Windows.Media.TranslateTransform
                     ?? new System.Windows.Media.TranslateTransform();
            TextArea.RenderTransform = tt;
            tt.X += dx;
            tt.Y += dy;
            _dragStart = current;
        }

        private void TextArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            TextArea.ReleaseMouseCapture();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ChangeFontSizeByFactor(double factor)
        {
            double newSize = Math.Max(10, Math.Min(200, ReminderText.FontSize * factor));
            AnimateFontSizeChange(newSize);
        }
    }
}
