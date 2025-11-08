using System;
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
        private int _currentTextIndex;
        private readonly string[] _texts;

        private readonly DoubleAnimation _fadeOutAnim;
        private readonly DoubleAnimation _fadeInAnim;

        public FloatingNoteWindow(Settings settings)
        {
            InitializeComponent();

            _texts = settings.Texts ?? new[] { "Default Note" };
            ReminderText.Text = _texts[0];
            ReminderText.FontSize = settings.StartFontSize;

            if (!settings.IsGlowEnabled)
            {
                ReminderText.Effect = null;
            }

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

            _textChangeTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(settings.IntervalSeconds)
            };
            _textChangeTimer.Tick += TextChangeTimer_Tick;

            if (_texts.Length > 1)
            {
                _textChangeTimer.Start();
            }
        }

        private void TextChangeTimer_Tick(object sender, EventArgs e)
        {
            int nextIndex = _currentTextIndex + 1;
            if (nextIndex >= _texts.Length) nextIndex = 0;

            if (_texts[nextIndex] != ReminderText.Text)
            {
                _currentTextIndex = nextIndex;
                ReminderText.BeginAnimation(OpacityProperty, _fadeOutAnim);
            }
        }

        private void FadeOutAnim_Completed(object sender, EventArgs e)
        {
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

        // --- HOVER LOGIC ---
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

        // --- POP DESTROY ANIMATION ---
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent double-clicks while animating
            CloseButton.IsEnabled = false;
            _textChangeTimer.Stop();

            // Create the "Pop" effect using keyframes
            // It goes from 1.0 -> 1.2 (fast pop up) -> 0.0 (shrink away)
            DoubleAnimationUsingKeyFrames popAnim = new DoubleAnimationUsingKeyFrames();
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
            popAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));

            // Fade out at the same time
            DoubleAnimation fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeAnim.BeginTime = TimeSpan.FromMilliseconds(50); // Start fading slightly after the pop starts

            Storyboard storyboard = new Storyboard();

            // Apply scale animation to X and Y axes of PopScale transform
            Storyboard.SetTarget(popAnim, TextArea);
            Storyboard.SetTargetProperty(popAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(popAnim);

            var popAnimY = popAnim.Clone();
            Storyboard.SetTarget(popAnimY, TextArea);
            Storyboard.SetTargetProperty(popAnimY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(popAnimY);

            // Apply fade animation to the whole TextArea
            Storyboard.SetTarget(fadeAnim, TextArea);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeAnim);

            // Close ONLY this window when done. The app remains running in the tray.
            storyboard.Completed += (s, args) => this.Close();
            storyboard.Begin();
        }

        // --- ZOOM LOGIC ---
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

        // --- TOUCH LOGIC ---
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

        // --- MOUSE DRAG LOGIC ---
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