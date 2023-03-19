#pragma warning disable CS8604

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Topify.Common
{
    internal class AnimationHandler
    {
        /// <summary>
        /// Fades in an inputted XAML object
        /// </summary>
        /// <param name="targetObject">The object to fade in</param>
        /// <param name="timeToFade">The amount time to fade the object in</param>
        public static void FadeIn(DependencyObject targetObject, double timeToFade)
        {
            var bC = targetObject;
            var fadeC = new DoubleAnimation()
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(timeToFade),
            };
            Storyboard.SetTarget(fadeC, bC);
            Storyboard.SetTargetProperty(fadeC, new PropertyPath(Button.OpacityProperty));
            var sbC = new Storyboard();
            sbC.Children.Add(fadeC);
            sbC.Begin();
        }

        /// <summary>
        /// Fades an inputted XAML object to any opacity
        /// </summary>
        /// <param name="targetObject">The object to fade in</param>
        /// <param name="timeToFade">The amount time to fade the object in</param>
        /// <param name="originatingOpacity">The opacity at which the animation will start</param>
        /// <param name="targetOpacity">The opacity at which the animation will end</param>
        public static void FadeAnimation(DependencyObject targetObject, double timeToFade, double originatingOpacity, double targetOpacity)
        {
            var b = targetObject;
            var fade = new DoubleAnimation()
            {
                From = originatingOpacity,
                To = targetOpacity,
                Duration = TimeSpan.FromSeconds(timeToFade),
            };
            Storyboard.SetTarget(fade, b);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Button.OpacityProperty));
            var sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Begin();
        }
        /// <summary>
        /// Fades out an inputted XAML object
        /// </summary>
        /// <param name="targetObject">The object to fade out</param>
        /// <param name="timeToFade">The amount time to fade the object out</param>
        public static void FadeOut(DependencyObject targetObject, double timeToFade)
        {
            var b = targetObject;
            var fade = new DoubleAnimation()
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(timeToFade),
            };
            Storyboard.SetTarget(fade, b);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Button.OpacityProperty));
            var sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Begin();
        }

        public static void BlurAnimation(DependencyObject targetObject, double timeLength, double from, double to)
        {

        }

        /// <summary>
        /// Moves an XAML object to any position over a set amount of time
        /// </summary>
        /// <param name="targetObject">The object to move</param>
        /// <param name="time">The amount of time it will take for the object to reach it's destination</param>
        /// <param name="from">The original position of the object. Can be object.Margin</param>
        /// <param name="to">The destination the object will get to at the end of the time inputted</param>
        public static void MovementAnimation(DependencyObject targetObject, double time, Thickness from, Thickness to)
        {
            var b = targetObject;
            var fade = new ThicknessAnimation()
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(time),
            };
            Storyboard.SetTarget(fade, b);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Button.MarginProperty));
            var sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Begin();
        }

        /// <summary>
        /// Animates color of a given XAML object
        /// </summary>
        /// <param name="targetObject">The object to animate color</param>
        /// <param name="time">The amount of time it will take for the object to reach the desired color</param>
        /// <param name="originatingColor">The origin color of the selected object</param>
        /// <param name="targetColor">The color the object will be at the end of the duration selected</param>
        public static void ColorAnimation(DependencyObject targetObject, Duration time, Brush originatingColor, Brush targetColor)
        {
            BrushAnimation ba = new BrushAnimation();
            ba.From = originatingColor;
            ba.To = targetColor;
            ba.Duration = time;
            Storyboard sb = new Storyboard();
            sb.BeginAnimation(Border.BackgroundProperty, ba);
        }
    }

    public class BrushAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType
        {
            get
            {
                return typeof(Brush);
            }
        }

        public override object GetCurrentValue(object defaultOriginValue,
                                               object defaultDestinationValue,
                                               AnimationClock animationClock)
        {
            return GetCurrentValue(defaultOriginValue as Brush,
                                   defaultDestinationValue as Brush,
                                   animationClock);
        }
        public object GetCurrentValue(Brush defaultOriginValue,
                                      Brush defaultDestinationValue,
                                      AnimationClock animationClock)
        {
            if (!animationClock.CurrentProgress.HasValue)
                return Brushes.Transparent;

            //use the standard values if From and To are not set 
            //(it is the value of the given property)
            defaultOriginValue = this.From ?? defaultOriginValue;
            defaultDestinationValue = this.To ?? defaultDestinationValue;

            if (animationClock.CurrentProgress.Value == 0)
                return defaultOriginValue;
            if (animationClock.CurrentProgress.Value == 1)
                return defaultDestinationValue;

            return new VisualBrush(new Border()
            {
                Width = 1,
                Height = 1,
                Background = defaultOriginValue,
                Child = new Border()
                {
                    Background = defaultDestinationValue,
                    Opacity = animationClock.CurrentProgress.Value,
                }
            });
        }

        protected override Freezable CreateInstanceCore()
        {
            return new BrushAnimation();
        }

        //we must define From and To, AnimationTimeline does not have this properties
        public Brush From
        {
            get { return (Brush)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }
        public Brush To
        {
            get { return (Brush)GetValue(ToProperty); }
            set { SetValue(ToProperty, value); }
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(Brush), typeof(BrushAnimation));
        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(Brush), typeof(BrushAnimation));
    }
}