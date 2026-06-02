using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MacroSpammer.UI.Timeline;

public static class TimelineAnimationService
{
    public static TranslateTransform EnsureTranslateTransform(UIElement element)
    {
        switch (element.RenderTransform)
        {
            case TranslateTransform translate:
                return translate;

            case TransformGroup group:
            {
                var existing = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (existing != null)
                    return existing;

                var translate = new TranslateTransform();
                group.Children.Add(translate);
                return translate;
            }

            case Transform existingTransform:
            {
                var group = new TransformGroup();
                group.Children.Add(existingTransform);

                var translate = new TranslateTransform();
                group.Children.Add(translate);

                element.RenderTransform = group;
                return translate;
            }

            default:
            {
                var translate = new TranslateTransform();
                element.RenderTransform = translate;
                return translate;
            }
        }
    }

    public static void AnimateRenderOffsetToRest(UIElement element, double deltaX, double deltaY, bool animateY)
    {
        var transform = EnsureTranslateTransform(element);
        var currentX = transform.X;
        var currentY = transform.Y;

        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);

        var startX = currentX + deltaX;
        var startY = currentY + deltaY;

        transform.X = startX;
        transform.Y = animateY ? startY : 0;

        var duration = new Duration(TimeSpan.FromMilliseconds(130));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation
            {
                From = startX,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            });

        if (!animateY)
            return;

        transform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = startY,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            });
    }
}
