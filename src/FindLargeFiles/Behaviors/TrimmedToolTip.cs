using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FindLargeFiles.Behaviors;

/// <summary>
/// Shows a TextBlock's full text as tooltip, but only while the text is cut off by TextTrimming.
/// Usage: <c>behaviors:TrimmedToolTip.IsEnabled="True"</c> on a TextBlock with TextTrimming set.
/// </summary>
public static class TrimmedToolTip
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TrimmedToolTip), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text) return;
        text.ToolTipOpening -= OnToolTipOpening;
        if (e.NewValue is not true) return;

        // A placeholder makes WPF raise ToolTipOpening; the real content is decided there.
        text.ToolTip ??= string.Empty;
        text.ToolTipOpening += OnToolTipOpening;
    }

    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        var text = (TextBlock)sender;
        if (!IsTrimmed(text))
        {
            e.Handled = true; // suppress
            return;
        }

        text.ToolTip = text.Text;
    }

    public static bool IsTrimmed(TextBlock text)
    {
        if (string.IsNullOrEmpty(text.Text)) return false;
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        var available = text.ActualWidth - text.Padding.Left - text.Padding.Right;
        return formatted.WidthIncludingTrailingWhitespace > available + 0.5;
    }
}
