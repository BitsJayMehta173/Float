using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FloatingNote
{
    public partial class FloatingNoteWindow : Window
    {
        private bool _isDragging;
        private Point _dragStart;
        private readonly DispatcherTimer _textChangeTimer;
        private int _currentIndex;
        // Now using the list of complex items
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
            _fadeOutAnim.Completed += FadeOutAnim_Completed;

            _fadeInAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            UpdateCloseButtonSize();

            // Initialize Timer (don't start yet if only 1 item)
            _textChangeTimer = new DispatcherTimer(DispatcherPriority.Background);
            _textChangeTimer.Tick += TextChangeTimer_Tick;

            // Start timer for the FIRST item's duration
            if (_items.Count > 1)
            {
                ResetTimerForCurrentItem();
            }
        }

        private void ResetTimerForCurrentItem()
        {
            _textChangeTimer.Stop();
            // Set interval based on the CURRENT item's desired duration
            _textChangeTimer.Interval = TimeSpan.FromSeconds(_items[_currentIndex].DurationSeconds);
            _textChangeTimer.Start();
        }

        private void TextChangeTimer_Tick(object sender, EventArgs e)
        {
            // Time is up for the current message. Fade it out.
            ReminderText.BeginAnimation(OpacityProperty, _fadeOutAnim);
        }

        private void FadeOutAnim_Completed(object sender, EventArgs e)
        {
            // Move to next index
            _currentIndex = (_currentIndex + 1) % _items.Count;

            // Update text
            ReminderText.Text = _items[_currentIndex].Message;

            // Fade back in
            ReminderText.BeginAnimation(OpacityProperty, _fadeInAnim);

            // IMPORTANT: Start the timer for the NEW item's duration
            ResetTimerForCurrentItem();
        }

        // =================================================================
        //  BELOW IS UNCHANGED UI/DRAG/ZOOM LOGIC from previous versions
        // =================================================================

        private void UpdateCloseButtonSize()
        {
            double ratio = 0.3;
            double btnSize = Math.Max(18, ReminderText.FontSize * ratio);
            CloseButton.Width = btnSize;
            CloseButton.Height = btnSize;
            CloseButton.FontSize = Math.Max(10, btnSize * 0.5);
        }

        private void ButtonHitbox_MouseEnter(object sender, MouseEventArgs e)
        {
            var anime = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            CloseButton.BeginAnimation(OpacityProperty, anime);
        }

        private void ButtonHitbox_MouseLeave(object sender, MouseEventArgs e)
        {
            var anime = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            CloseButton.BeginAnimation(OpacityProperty, anime);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseButton.IsEnabled = false;
            _textChangeTimer.Stop();

            DoubleAnimationUsingKeyFrames popAnim = new DoubleAnimationUsingKeyFrames();
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));

            DoubleAnimation fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeAnim.BeginTime = TimeSpan.FromMilliseconds(50);

            Storyboard storyboard = new Storyboard();

            Storyboard.SetTarget(popAnim, TextArea);
            Storyboard.SetTargetProperty(popAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(popAnim);

            var popAnimY = popAnim.Clone();
            Storyboard.SetTarget(popAnimY, TextArea);
            Storyboard.SetTargetProperty(popAnimY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(popAnimY);

            Storyboard.SetTarget(fadeAnim, TextArea);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeAnim);

            storyboard.Completed += (s, args) => this.Close();
            storyboard.Begin();
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            const double step = 5.0;
            double delta = e.Delta > 0 ? step : -step;
            double newSize = Math.Max(10, Math.Min(400, ReminderText.FontSize + delta));
            AnimateFontSizeChange(newSize);
        }

        private void AnimateFontSizeChange(double targetSize)
        {
            double from = ReminderText.FontSize;
            if (Math.Abs(targetSize - from) < 0.1) return;

            var anim = new DoubleAnimation(from, targetSize, TimeSpan.FromMilliseconds(80))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (s, e) =>
            {
                ReminderText.FontSize = targetSize;
                UpdateCloseButtonSize();
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