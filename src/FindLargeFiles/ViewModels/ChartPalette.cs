using System.Windows;
using System.Windows.Media;

namespace FindLargeFiles.ViewModels;

/// <summary>
/// Categorical colors for the dark surface, validated for color-vision deficiency separation.
/// Slots are assigned in fixed order; anything beyond eight folds into "Andere".
/// </summary>
public static class ChartPalette
{
    public const int MaxSeries = 8;

    public static readonly Brush[] Series =
    [
        Freeze("#3987E5"), Freeze("#D95926"), Freeze("#199E70"), Freeze("#C98500"),
        Freeze("#D55181"), Freeze("#008300"), Freeze("#9085E9"), Freeze("#E66767"),
    ];

    public static readonly Brush Other = Freeze("#5F667A");

    /// <summary>Hatched slate, so free space reads as "not data" even without color.</summary>
    public static readonly Brush FreeSpace = CreateHatch();

    public static readonly Brush Single = Series[0];

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateHatch()
    {
        var background = new GeometryDrawing(Freeze("#2E3446"), null, new RectangleGeometry(new Rect(0, 0, 8, 8)));
        var line = new GeometryDrawing(null, new Pen(Freeze("#4A5268"), 1.5), new LineGeometry(new Point(0, 8), new Point(8, 0)));
        var brush = new DrawingBrush(new DrawingGroup { Children = { background, line } })
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }
}
