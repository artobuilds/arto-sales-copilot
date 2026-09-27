using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArtoSalesCopilot;

// Presentation-only transitions. SnapshotAndReplace prevents animation queues.
internal static class UiMotion
{
    public static readonly DependencyProperty ObserveProperty = DependencyProperty.RegisterAttached(
        "Observe", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, Changed));
    public static void SetObserve(DependencyObject target, bool value) => target.SetValue(ObserveProperty, value);
    public static bool GetObserve(DependencyObject target) => (bool)target.GetValue(ObserveProperty);
    internal static bool KeyboardInput { get; set; }
    internal static bool SuppressForTest { get; set; }
    internal static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast && !KeyboardInput && !SuppressForTest;

    static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is ComboBox combo)
        {
            combo.DropDownOpened -= Opened;
            if ((bool)e.NewValue) combo.DropDownOpened += Opened;
        }
        if (target is Expander expander)
        {
            expander.Expanded -= Expanded;
            if ((bool)e.NewValue) expander.Expanded += Expanded;
        }
    }
    static void Opened(object? sender, EventArgs e)
    {
        if (sender is ComboBox combo && combo.Template.FindName("PART_Popup", combo) is Popup popup && popup.Child is FrameworkElement child) Reveal(child, 140);
    }
    static void Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is Expander expander && e.Source == expander && expander.Content is FrameworkElement content) Reveal(content, 160);
    }
    internal static void Reveal(FrameworkElement element, int milliseconds = 180)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (element.RenderTransform is not TranslateTransform transform) element.RenderTransform = transform = new TranslateTransform();
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        element.Opacity = 1; transform.Y = 0;
        if (!Enabled) return;
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.7, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop }, HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop }, HandoffBehavior.SnapshotAndReplace);
    }
}
