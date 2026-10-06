using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FindLargeFiles.ViewModels;

namespace FindLargeFiles;

/// <summary>True when the bound value equals the parameter; converts back to the parameter when checked.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null && parameter != null && value.ToString() == parameter.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter == null) return Binding.DoNothing;
        var text = parameter.ToString()!;
        return targetType.IsEnum ? Enum.Parse(targetType, text) : System.Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}

public sealed class VisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            int i => i > 0,
            System.Collections.ICollection c => c.Count > 0,
            null => false,
            _ => true,
        };
        return visible ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Multiplies a 0..1 fraction (first value) with an available width (second value).</summary>
public sealed class FractionWidthConverter : IMultiValueConverter
{
    public double Minimum { get; set; } = 2;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double fraction || values[1] is not double width) return 0d;
        if (fraction <= 0) return 0d;
        return Math.Max(Minimum, Math.Min(width, fraction * width));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class RowIconGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RowIcon.Drive => "",
        RowIcon.Folder => "",
        RowIcon.Locked => "",
        RowIcon.Link => "",
        RowIcon.More => "",
        _ => "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class RowIconBrushConverter : IValueConverter
{
    public Brush Folder { get; set; } = Brushes.Goldenrod;
    public Brush File { get; set; } = Brushes.Gray;
    public Brush Drive { get; set; } = Brushes.SteelBlue;
    public Brush Warning { get; set; } = Brushes.OrangeRed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RowIcon.Drive => Drive,
        RowIcon.Folder => Folder,
        RowIcon.Locked or RowIcon.Link => Warning,
        _ => File,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Maps a drive's usage level ("normal"/"warning"/"critical") to a brush.</summary>
public sealed class LevelBrushConverter : IValueConverter
{
    public Brush Normal { get; set; } = Brushes.SteelBlue;
    public Brush Warning { get; set; } = Brushes.Orange;
    public Brush Critical { get; set; } = Brushes.Red;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "critical" => Critical,
        "warning" => Warning,
        _ => Normal,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
