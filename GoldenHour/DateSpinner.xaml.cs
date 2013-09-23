using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Media3D;
using Windows.UI.Xaml.Navigation;

namespace GoldenHour.Common
{
    public sealed partial class DateSpinner : UserControl
    {
        private double radius = 1000;
        private double midPoint;
        private double visibleWidth;
        private double visibleHeight;
        private double visibleAngleRange;
        private ButtonInfo leftMost;
        private ButtonInfo rightMost;
        private double startAngle;
        private int cacheCount; // how many buttons to create either side of center button
        private ButtonInfo selection; // what button is currently in the center?
        private bool firstLayout = true;
        private AngularIndex index = new AngularIndex();

        private const double FlickRotateDeceleration = 0.01;

        const double RadiansToDegrees = 180 / Math.PI;

        public DateSpinner()
        {
            this.InitializeComponent();

            this.SizeChanged += DateSpinner_SizeChanged;

            this.ManipulationMode = ManipulationModes.All;

            this.SelectedDate = DateTime.Today;
        }

        public DateTime SelectedDate
        {
            get { return (DateTime)GetValue(SelectedDateProperty); }
            set { SetValue(SelectedDateProperty, value); }

        }

        // Using a DependencyProperty as the backing store for SelectedDate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register("SelectedDate", typeof(DateTime), typeof(DateSpinner), new PropertyMetadata(DateTime.Today, OnDateChanged));


        private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DateSpinner ds = (DateSpinner)d;
            ds.OnDateChanged((DateTime)e.NewValue);
        }

        private void OnDateChanged(DateTime date)
        {
            ButtonInfo found = FindButtonByDate(date);
            if (found != null)
            {
                if (found != selection)
                {
                    SelectButton(found);
                    ScrollToCenter(found);
                }
            }
            else
            {
                // repopulate buttons around this selected date, which could be anywhere,
                // so discard existing buttons since they could be irrelevant.
                if (this.selection != null)
                {
                    this.Angle = 0;
                    firstLayout = true;
                    index.Clear();
                    dateIndex.Clear();
                    SpinnerPanel.Children.Clear();

                    // first button bootstrap (SelectedDate is not up to date yet, so we have to do this here).
                    selection = new ButtonInfo() { Date = date };
                    CreateButton(selection);
                    SelectButton(selection);
                    leftMost = rightMost = selection;

                    Populate(visibleWidth, true, true);
                }
            }
			
        }

        Dictionary<DateTime, ButtonInfo> dateIndex = new Dictionary<DateTime, ButtonInfo>();

        private ButtonInfo FindButtonByDate(DateTime date)
        {
            ButtonInfo found = null;
            dateIndex.TryGetValue(date, out found);
            return found;
        }

        /// <summary>
        /// Angle of the wheel, note: we allow angle to increase and decrease infinitely in order to reach all possible dates.
        /// This property can be animated.
        /// </summary>
        public double Angle
        {
            get { return (double)GetValue(AngleProperty); }
            set { SetValue(AngleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Angle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AngleProperty =
            DependencyProperty.Register("Angle", typeof(double), typeof(DateSpinner), new PropertyMetadata(0.0, OnAngleChanged));

        private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DateSpinner ds = (DateSpinner)d;
            ds.RotateTo(ds.Angle, true);
        }

        protected override void OnManipulationInertiaStarting(ManipulationInertiaStartingRoutedEventArgs e)
        {
            e.TranslationBehavior.DesiredDeceleration = FlickRotateDeceleration;
            e.Handled = true;
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            startAngle = this.Angle;
            base.OnManipulationStarted(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            Point delta = e.Cumulative.Translation;
            double dx = -delta.X;

            // how much of the circle is this?
            double angleDelta = Math.Asin(Math.Max(-1, Math.Min(1, dx / (2 * radius))));
            RotateTo(startAngle + angleDelta, true);

            e.Handled = true;
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            Point delta = e.Cumulative.Translation;
            double dx = -delta.X;
            double angleDelta = Math.Asin(Math.Max(-1, Math.Min(1, dx / (2 * radius))));

            this.Angle = this.targetAngle = startAngle + angleDelta;

            e.Handled = true;

            RemoveHiddenButtons();
        }

        private void RemoveHiddenButtons()
        {
            List<Button> hidden = new List<Button>();
            foreach (Button child in SpinnerPanel.Children)
            {
                if (child.Opacity == 0)
                {
                    hidden.Add(child);
                }
                else
                {
                    // force repaint so left over mouse over effects are cleaned up.
                    child.Background = child.Background;
                }
            }

            foreach (Button b in hidden)
            {
                // we keep the ButtonInfo structure around so we can remember the sizes
                // of the buttons which helps us with Layout.
                ButtonInfo info = (ButtonInfo)b.Tag;
                info.Button = null;
                SpinnerPanel.Children.Remove(b);
            }

        }

        void DateSpinner_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Size s = e.NewSize;
            visibleHeight = s.Height;

            double width = s.Width;
            bool changed = (this.visibleWidth != width);
            radius = width * 2;

            // since radius is a multiple of width, (width / 2 * radius) = 1/4.
            visibleAngleRange = 2 * Math.Asin(0.25);

            this.visibleWidth = width;
            this.midPoint = visibleWidth / 2;

            if (changed || SpinnerPanel.Children.Count == 0)
            {
                Populate(width, true, true);
            }
        }

        private void RotateTo(double newAngle, bool extend = false)
        {
            double leftAngle = -newAngle + (visibleAngleRange / 2);
            double rightAngle = leftAngle - visibleAngleRange;

            bool needMoreToLeft = false;
            bool needMoreToRight = true;

            // find left most visible button
            ButtonInfo info = index.GetNearest(leftAngle);
            if (info == null)
            {
                needMoreToLeft = true;
            }
            else
            {
                HashSet<ButtonInfo> existing = new HashSet<ButtonInfo>();

                foreach (Button b in SpinnerPanel.Children)
                {
                    existing.Add((ButtonInfo)b.Tag);
                }

                // one more to be sure we are off the left edge.
                if (info.Previous != null)
                {
                    info = info.Previous;
                }

                if (info.Angle < leftAngle)
                {
                    needMoreToLeft = true;
                }

                // now scan to the right repositioning all the visible buttons.
                while (info != null)
                {
                    existing.Remove(info);

                    if (info.AngleSpan > 0)
                    {
                        double originalAngle = info.Angle;

                        double angle = originalAngle + newAngle;

                        Button b = info.Button;
                        if (b == null)
                        {
                            // re-realize button, the old info.AngleSpan should be good.
                            b = CreateButton(info);
                            if (info == selection)
                            {
                                // set border
                                SelectButton(info);
                            }
                        }

                        RotateAboutYAxis(b, angle);

                        // ok, button is now in the right place so we can make it visible.
                        b.Opacity = 1;
                        b.IsHitTestVisible = true;

                        if (originalAngle < rightAngle)
                        {
                            // done.
                            needMoreToRight = false;
                            break;
                        }
                    }

                    info = info.Next;
                }

                // virtualize any children that are no longer needed
                foreach (ButtonInfo b in existing)
                {
                    b.Button.Opacity = 0;
                    b.Button.IsHitTestVisible = false;
                }
            }

            if (extend && (needMoreToRight || needMoreToLeft))
            {
                Populate(visibleWidth, needMoreToLeft, needMoreToRight);
            }
        }

        private void SelectButton(ButtonInfo buttonInfo)
        {
            if (this.selection != null)
            {
                Button button = this.selection.Button;
                if (button != null)
                {
                    Brush brush = this.FindResource<Brush>("FocusVisualWhiteStrokeThemeBrush");
                    button.BorderBrush = brush;
                }
            }

            this.selection = buttonInfo;

            if (this.selection != null)
            {
                SelectedDate = buttonInfo.Date;

                Button button = this.selection.Button;
                if (button != null)
                {
                    Brush brush = this.FindResource<Brush>("SelectedVisualStrokeThemeBrush");
                    button.BorderBrush = brush;
                }
            }
        }

        /// <summary>
        /// This method ensures we have enough buttons either side of the center focus
        /// to fill the given control width.
        /// </summary>
        /// <param name="width">The width of this control</param>
        private void Populate(double width, bool addMoreToLeft, bool addMoreToRight)
        {
            if (width == 0)
            {
                // not ready yet.
                return;
            }

            bool added = false;

            int count = cacheCount;
            if (count == 0)
            {
                // no estimate yet, so assume each button is about 50 pixels wide
                count = cacheCount = (int)(width / 50);
                if (count == 0) count = 1;
            }

            if (selection == null)
            {
                // first button bootstrap.
                selection = new ButtonInfo() { Date = this.SelectedDate.Date };
                CreateButton(selection);
                SelectButton(selection);
                leftMost = rightMost = selection;
            }

            if (addMoreToRight)
            {
                DateTime d = rightMost.Date.AddDays(1);

                // ensure we have 'count' buttons to the right of the current central focus button.
                for (int i = 0; i < count; i++)
                {
                    added = true;
                    ButtonInfo info = new ButtonInfo() { Date = d };
                    Button b = CreateButton(info);
                    b.Opacity = 0; // start out invisible until we Layout so button doesn't show up in wrong place.
                    b.IsHitTestVisible = false;

                    // link in the new button
                    rightMost.Next = info;
                    info.Previous = rightMost;
                    rightMost = info;

                    d = d.AddDays(1);
                }
            }

            if (addMoreToLeft)
            {
                DateTime d = leftMost.Date.AddDays(-1);

                // ensure we have 'count' buttons to the left of the current central focus button.
                for (int i = 0; i < count; i++)
                {
                    added = true;
                    ButtonInfo info = new ButtonInfo() { Date = d };
                    Button b = CreateButton(info);
                    b.Opacity = 0; // start out invisible until we Layout so button doesn't show up in wrong place.
                    b.IsHitTestVisible = false;

                    // link in the new button
                    leftMost.Previous = info;
                    info.Next = leftMost;
                    leftMost = info;

                    d = d.AddDays(-1);
                }
            }

            if (added)
            {
                InvalidateArrange();
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            this.visibleWidth = finalSize.Width;
            this.midPoint = visibleWidth / 2;

            // arrange the canvas.
            Size result = base.ArrangeOverride(finalSize);

            Layout();

            SpinnerPanel.Clip = new RectangleGeometry()
            {
                Rect = new Rect(0, 0, finalSize.Width, finalSize.Height)
            };

            return result;
        }

        /// <summary>
        /// Project the buttons onto a drum of size "radius" where DateTime.Today is in the center (angle=0).
        /// all other dates radiate out from there.  These angles are fixed, we then change the master Angle
        /// property to rotate the wheel and RotateTo computes the new projections by adding the two angles.
        /// </summary>
        private void Layout()
        {
            if (selection != null)
            {
                if (firstLayout)
                {
                    // ok, normally we layout from existing button positions, but since this is the very
                    // first time, we start with the selection and center it, then work out from there.

                    selection.Angle = 0;

                    // approximate how much of the wheel surface this button covers in angular distance.
                    Button b = selection.Button;
                    Debug.Assert(b != null);

                    double buttonWidth = b.DesiredSize.Width;
                    double angleSpan = 2 * Math.Asin(buttonWidth / (2 * radius));
                    selection.AngleSpan = angleSpan;

                    // now we can index this one.
                    index.Add(selection);
                }

                ButtonInfo start = null;

                // find the first child button that has an angle we can start from.
                foreach (Button b in SpinnerPanel.Children)
                {
                    ButtonInfo info = (ButtonInfo)b.Tag;
                    if (info.AngleSpan > 0)
                    {
                        start = info;
                        break;
                    }
                }

                Debug.Assert(start != null);

                // next position to the right (subtraction is to the right)
                double nextAngle = start.Angle - start.AngleSpan;

                // ok, now we can work from the start position to the right, adding each subsequent button

                for (ButtonInfo info = start.Next; info != null; info = info.Next)
                {
                    if (info.AngleSpan == 0)
                    {
                        Button b = info.Button;
                        if (b != null)
                        {
                            // Note: the right hand side is the negative axis on the angles which is why
                            // we are subtracting the angleSpans in this case.  The left hand side is the positive angles.
                            info.Angle = nextAngle;

                            // approximate how much of the wheel surface this button covers in angular distance.
                            double buttonWidth = b.DesiredSize.Width;
                            double angleSpan = 2 * Math.Asin(buttonWidth / (2 * radius));
                            info.AngleSpan = angleSpan;

                            index.Add(info);
                        }
                        else
                        {
                            // reached virtualized range, so we're done.
                            break;
                        }
                    }
                    else
                    {
                        // existing button, we can just continue with it's already computed span.
                        nextAngle = info.Angle;
                    }

                    nextAngle -= info.AngleSpan;
                }

                nextAngle = start.Angle;

                // Left hand side of the wheel.
                for (ButtonInfo info = start.Previous; info != null; info = info.Previous)
                {
                    if (info.AngleSpan == 0)
                    {
                        Button b = info.Button;
                        if (b != null)
                        {
                            // approximate how much of the wheel surface this button covers in angular distance.
                            double buttonWidth = b.DesiredSize.Width;
                            double angleSpan = 2 * Math.Asin(buttonWidth / (2 * radius));
                            info.AngleSpan = angleSpan;

                            nextAngle += angleSpan;

                            info.Angle = nextAngle;

                            index.Add(info);
                        }
                        else
                        {
                            // reached virtualized range, so we're done.
                            break;
                        }
                    }
                    else
                    {
                        // existing button, we can just continue with it's already computed span.
                        nextAngle = info.Angle;
                    }
                }
            }

            // RotateTo will hide any buttons that are off the edge of the visible region.

            if (firstLayout && selection != null)
            {
                firstLayout = false;
                // rotate left half the width of the selection so it is centered.                
                ButtonInfo info = (ButtonInfo)selection;
                double angleSpan = info.AngleSpan;
                this.Angle = angleSpan / 2;
            }
            else
            {
                RotateTo(this.Angle);
            }
        }

        private void RotateAboutYAxis(Button b, double angle)
        {
            double degrees = angle * RadiansToDegrees;
            PlaneProjection pp = (PlaneProjection)b.Projection;
            if (pp == null)
            {
                b.Projection = CreateRotateYProjection(degrees);
            }
            else if (pp.RotationY != degrees)
            {
                pp.RotationY = degrees;
            }
        }

        private Projection CreateRotateYProjection(double degrees)
        {
            Debug.Assert(midPoint != 0);
            if (midPoint == 0)
            {
                midPoint = 1;
            }

            PlaneProjection pp = new PlaneProjection()
            {
                RotationY = degrees,
                LocalOffsetX = midPoint,
                CenterOfRotationX = radius / midPoint,
                CenterOfRotationY = 0,
                CenterOfRotationZ = -radius
            };
            return pp;
        }

        private Button CreateButton(ButtonInfo info)
        {
            Button b = new Button()
            {
                VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Stretch
            };

            b.Click += OnButtonClick;

            TextBlock content = new TextBlock()
            {
                TextAlignment = TextAlignment.Center
            };

            DateTime date = info.Date;

            content.Inlines.Add(new Run() { Text = date.ToString("ddd") });
            content.Inlines.Add(new LineBreak());
            content.Inlines.Add(new Run() { Text = date.ToString("dd") });
            content.Inlines.Add(new LineBreak());
            content.Inlines.Add(new Run() { Text = date.ToString("MMM") });

            b.Content = content;

            info.Button = b;
            b.Tag = info;

            if (date == DateTime.Today)
            {
                b.Background = this.FindResource<Brush>("TodaysDateBackgroundThemeBrush");
            }

            SpinnerPanel.Children.Add(b);

            // update index by date
            dateIndex[date.Date] = info;

            return b;

        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            ButtonInfo info = (ButtonInfo)b.Tag;
            SelectButton(info);
            ScrollToCenter(info);
        }

        private void ScrollToCenter(ButtonInfo info)
        {
            if (info.AngleSpan == 0)
            {
                // no layout yet, so can't scroll to anywhere.
                return;
            }
            AnimateAngle(-info.Angle + info.AngleSpan / 2, new Duration(TimeSpan.FromSeconds(.3)));
        }

        double targetAngle;
        Storyboard storyboard;

        private void StopAnimation()
        {
            if (storyboard != null)
            {
                storyboard.Stop();
                storyboard = null;
            }
        }

        public void AnimateAngle(double newAngle, Duration duration)
        {
            targetAngle = newAngle;
            StopAnimation();

            DoubleAnimation animation = new DoubleAnimation()
            {
                To = newAngle,
                Duration = duration,
                EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut }
            };

            animation.Completed += OnAnimationCompleted;

            // for some weird reason, by default WinRT does NOT run this animation because it thinks
            // it's targeting a property that will slow down the UI too much, so we override that here.
            animation.EnableDependentAnimation = true;

            storyboard = new Storyboard();
            storyboard.FillBehavior = FillBehavior.HoldEnd;
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, "Angle");
            storyboard.Begin();
        }

        private void OnAnimationCompleted(object sender, object e)
        {
            this.Angle = targetAngle;
            RemoveHiddenButtons();
        }

        /// <summary>
        /// This helper class keeps a doubly linked list of the buttons in date order so we can quickly
        /// rotate and virtualize the list of buttons.
        /// </summary>
        private class ButtonInfo
        {
            private ButtonInfo next;
            private ButtonInfo prev;

            /// <summary>
            /// The button at this location.  This could be null if we have virtualized this location.
            /// </summary>
            public Button Button;

            /// <summary>
            /// The date shown on the button.
            /// </summary>
            public DateTime Date;

            /// <summary>
            /// The angle location of the button (in radians)
            /// </summary>
            public double Angle;

            /// <summary>
            /// The amount of space taken on the wheel by this button (in radians)
            /// </summary>
            public double AngleSpan;

            /// <summary>
            /// The next button in the linked list or null.
            /// </summary>
            public ButtonInfo Next
            {
                get { return next; }
                set { next = value; }
            }

            /// <summary>
            /// The previous button in the linked list or null.
            /// </summary>
            public ButtonInfo Previous
            {
                get { return prev; }
                set { prev = value; }
            }

        }

        /// <summary>
        /// This class stores ButtonInfo objects that fall within a specified angular quadrant of the wheel.
        /// The quadrants are each 45 degrees and are numbered 0 for first 45 degrees, 1 for the next 45 degrees
        /// and so on, including negative numbers for the reverse direction.
        /// </summary>
        private class AngularIndex
        {
            private Dictionary<int, HashSet<ButtonInfo>> index = new Dictionary<int, HashSet<ButtonInfo>>();

            public void Clear()
            {
                index.Clear();
            }

            public void Add(ButtonInfo info)
            {
                // buttons must be laid out before they are added
                Debug.Assert(info.AngleSpan != 0);

                double degrees = info.Angle * 180 / Math.PI;

                int quadrant = (int)(degrees / 45);

                HashSet<ButtonInfo> bucket = null;
                if (!index.TryGetValue(quadrant, out bucket))
                {
                    bucket = new HashSet<ButtonInfo>();
                    index[quadrant] = bucket;
                }
                bucket.Add(info);
            }

            /// <summary>
            /// Get the button info nearest the given angle.
            /// </summary>
            /// <param name="angleInRadians">The angle we're looking for</param>
            /// <returns>The nearest button info or null if this index is empty</returns>
            public ButtonInfo GetNearest(double angleInRadians)
            {
                double degrees = angleInRadians * 180 / Math.PI;

                int quadrant = (int)(degrees / 45);

                HashSet<ButtonInfo> bucket = null;
                ButtonInfo nearest = null;
                double distance = double.MaxValue;

                if (index.TryGetValue(quadrant, out bucket))
                {
                    foreach (ButtonInfo info in bucket)
                    {
                        double d = Math.Abs(info.Angle - angleInRadians);
                        if (d < distance)
                        {
                            nearest = info;
                            distance = d;
                        }
                    }
                    Debug.Assert(nearest != null, "Buckets should never be empty");
                    return nearest;
                }

                // search all buckets then.
                foreach (HashSet<ButtonInfo> values in index.Values)
                {
                    foreach (ButtonInfo info in values)
                    {
                        double d = Math.Abs(info.Angle - angleInRadians);
                        if (d < distance)
                        {
                            nearest = info;
                            distance = d;
                        }
                    }
                }

                return nearest;
            }
        }
    }
}
