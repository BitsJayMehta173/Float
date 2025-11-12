using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FloatingReminder
{
    public partial class FloatingNoteWindow : Window
    {
        private bool _isDragging;
        private Point _dragStart;
        private readonly DispatcherTimer _textChangeTimer;
        private int _currentIndex;
        private readonly List<ReminderItem> _items;

        private readonly DoubleAnimation _fadeOutAnim;
        private readonly DoubleAnimation _fadeInAnim;

        public FloatingNoteWindow(Settings settings)
        {
            InitializeComponent();

            _items = settings.Items ?? new List<ReminderItem>();
            if (_items.Count == 0) _items.Add(new ReminderItem { Message = "No messages configured.", DurationSeconds = 10 });

            _currentIndex = 0;

            // Initial Setup
            ReminderText.Text = _items[0].Message;
            ReminderText.FontSize = settings.StartFontSize;
            if (!settings.IsGlowEnabled) ReminderText.Effect = null;

            // Animations
            _fadeOutAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            _fadeOutAnim.Completed += FadeOut_Completed;

            _fadeInAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            _fadeInAnim.Completed += (s, e) => ReminderText.Opacity = 1;

            // Timer
            _textChangeTimer = new DispatcherTimer();
            _textChangeTimer.Tick += TextChangeTimer_Tick;
            StartTimerForItem(_currentIndex);
        }

        private void StartTimerForItem(int index)
        {
            int durationSeconds = _items[index].DurationSeconds;
            _textChangeTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _textChangeTimer.Start();
        }

        // FIX: This method body was corrupted. It is now fixed.
        private void TextChangeTimer_Tick(object sender, EventArgs e)
        {
            ReminderText.BeginAnimation(OpacityProperty, _fadeOutAnim);
        }

        private void FadeOut_Completed(object sender, EventArgs e)
        {
            ReminderText.Opacity = 0;
            _currentIndex = (_currentIndex + 1) % _items.Count;
            ReminderText.Text = _items[_currentIndex].Message;
            ReminderText.BeginAnimation(OpacityProperty, _fadeInAnim);
            StartTimerForItem(_currentIndex);
        }

        // --- UI Event Handlers ---

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _textChangeTimer.Stop();
            // Animate window fade out before closing
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, a) => this.Close();
            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ButtonHitbox_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseButton.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void ButtonHitbox_MouseLeave(object sender, MouseEventArgs e)
        {
            CloseButton.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)));
        }

        private void UpdateCloseButtonSize()
        {
            double scale = ReminderText.FontSize / 60.0;
            double newSize = Math.Max(24, 36 * scale);
            CloseButton.Width = newSize;
            CloseButton.Height = newSize;
            CloseButton.FontSize = Math.Max(12, 18 * scale);
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double newSize = ReminderText.FontSize + (e.Delta > 0 ? 4 : -4);
            newSize = Math.Max(10, Math.Min(400, newSize));
            AnimateFontSize(newSize);
            UpdateCloseButtonSize();
        }

        private void AnimateFontSize(double newSize)
        {
            var anim = new DoubleAnimation(ReminderText.FontSize, newSize, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ReminderText.BeginAnimation(TextBlock.FontSizeProperty, anim);
        }

        private void Window_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (e.DeltaManipulation.Scale.X != 1.0)
            {
                double newSize = ReminderText.FontSize * e.DeltaManipulation.Scale.X;
                newSize = Math.Max(10, Math.Min(400, newSize));
                ReminderText.FontSize = newSize;
                UpdateCloseButtonSize();
            }

            DragTranslate.X += e.DeltaManipulation.Translation.X;
            DragTranslate.Y += e.DeltaManipulation.Translation.Y;
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

            DragTranslate.X += dx;
            DragTranslate.Y += dy;

            _dragStart = current;
        }

        private void TextArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            TextArea.ReleaseMouseCapture();
        }
    }
}