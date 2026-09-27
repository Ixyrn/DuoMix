using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace DuoMix;
public static class Motion
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation;
    static DoubleAnimation Tween(double from, double to, double ms) => new(from, to, TimeSpan.FromMilliseconds(Enabled ? ms : 1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
    public static void Pop(FrameworkElement element) { element.RenderTransformOrigin = new Point(.5, .5); var scale = new ScaleTransform(1, 1); element.RenderTransform = scale; scale.BeginAnimation(ScaleTransform.ScaleXProperty, Tween(.95, 1, 190)); scale.BeginAnimation(ScaleTransform.ScaleYProperty, Tween(.95, 1, 190)); element.BeginAnimation(UIElement.OpacityProperty, Tween(0, 1, 160)); }
    public static Task FadeOut(UIElement element) { var done = new TaskCompletionSource(); var anim = Tween(element.Opacity, 0, 110); anim.Completed += (_, _) => done.TrySetResult(); element.BeginAnimation(UIElement.OpacityProperty, anim); return done.Task; }
    public static void Expand(FrameworkElement element, bool expand) { double from = element.Visibility == Visibility.Collapsed ? 0 : element.ActualHeight; element.BeginAnimation(FrameworkElement.HeightProperty, null); element.Height = double.NaN; element.Visibility = Visibility.Visible; element.ClipToBounds = true; element.Measure(new Size(Math.Max(1, element.ActualWidth), double.PositiveInfinity)); double to = expand ? element.DesiredSize.Height : 0; var anim = Tween(from, to, 190); anim.Completed += (_, _) => { element.BeginAnimation(FrameworkElement.HeightProperty, null); element.Height = double.NaN; if (!expand) element.Visibility = Visibility.Collapsed; }; element.BeginAnimation(FrameworkElement.HeightProperty, anim); }
}
