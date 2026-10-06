using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FindLargeFiles.Core;
using FindLargeFiles.ViewModels;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Controls;

/// <summary>Draws a size breakdown as pie, donut, horizontal bars or a squarified treemap.</summary>
public sealed class SizeChart : FrameworkElement
{
    private const double Gap = 2;

    public static readonly DependencyProperty ItemsProperty = Register(nameof(Items), typeof(IReadOnlyList<ChartItem>), null);
    public static readonly DependencyProperty ModeProperty = Register(nameof(Mode), typeof(ChartMode), ChartMode.Donut);
    public static readonly DependencyProperty SurfaceBrushProperty = Register(nameof(SurfaceBrush), typeof(Brush), Brushes.Black);
    public static readonly DependencyProperty ForegroundProperty = Register(nameof(Foreground), typeof(Brush), Brushes.White);
    public static readonly DependencyProperty SecondaryForegroundProperty = Register(nameof(SecondaryForeground), typeof(Brush), Brushes.Gray);
    public static readonly DependencyProperty TooltipBackgroundProperty = Register(nameof(TooltipBackground), typeof(Brush), Brushes.Black);
    public static readonly DependencyProperty TooltipBorderBrushProperty = Register(nameof(TooltipBorderBrush), typeof(Brush), Brushes.Gray);
    public static readonly DependencyProperty HoverBrushProperty = Register(nameof(HoverBrush), typeof(Brush), Brushes.Transparent);
    public static readonly DependencyProperty CenterLabelProperty = Register(nameof(CenterLabel), typeof(string), string.Empty);

    public static readonly DependencyProperty ItemCommandProperty = DependencyProperty.Register(
        nameof(ItemCommand), typeof(ICommand), typeof(SizeChart));

    private readonly List<(Geometry Shape, int Index)> _hitAreas = [];
    private int _hover = -1;
    private Point _mouse;

    private static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(SizeChart),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((SizeChart)d)._hover = -1));

    public IReadOnlyList<ChartItem>? Items { get => (IReadOnlyList<ChartItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public ChartMode Mode { get => (ChartMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Brush SecondaryForeground { get => (Brush)GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    public Brush TooltipBackground { get => (Brush)GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public Brush TooltipBorderBrush { get => (Brush)GetValue(TooltipBorderBrushProperty); set => SetValue(TooltipBorderBrushProperty, value); }
    public Brush HoverBrush { get => (Brush)GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public string CenterLabel { get => (string)GetValue(CenterLabelProperty); set => SetValue(CenterLabelProperty, value); }
    public ICommand? ItemCommand { get => (ICommand?)GetValue(ItemCommandProperty); set => SetValue(ItemCommandProperty, value); }

    // ───────────── Rendering ─────────────

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(Brushes.Transparent, null, bounds);
        _hitAreas.Clear();

        var items = Items;
        if (items == null || items.Count == 0 || bounds.Width < 40 || bounds.Height < 40) return;

        switch (Mode)
        {
            case ChartMode.Pie:
            case ChartMode.Donut:
                RenderPie(dc, items, bounds, Mode == ChartMode.Donut);
                break;
            case ChartMode.Bar:
                RenderBars(dc, items, bounds);
                break;
            case ChartMode.Treemap:
                RenderTreemap(dc, items, bounds);
                break;
        }

        if (_hover >= 0 && _hover < items.Count) RenderTooltip(dc, items[_hover], bounds);
    }

    private void RenderPie(DrawingContext dc, IReadOnlyList<ChartItem> items, Rect bounds, bool donut)
    {
        var radius = Math.Min(170, Math.Min(bounds.Width, bounds.Height) / 2 - 10);
        if (radius < 20) return;
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var inner = donut ? radius * 0.64 : 0;
        var gapPen = new Pen(SurfaceBrush, Gap);
        gapPen.Freeze();

        var start = -90.0;
        for (var i = 0; i < items.Count; i++)
        {
            var sweep = items[i].Share * 360;
            if (sweep <= 0) continue;

            var shape = Segment(center, radius, inner, start, sweep);
            _hitAreas.Add((shape, i));

            var drawn = shape;
            if (i == _hover && sweep < 359)
            {
                var mid = (start + sweep / 2) * Math.PI / 180;
                drawn = shape.Clone();
                drawn.Transform = new TranslateTransform(Math.Cos(mid) * 6, Math.Sin(mid) * 6);
            }

            dc.DrawGeometry(items[i].Brush, items.Count > 1 ? gapPen : null, drawn);
            start += sweep;
        }

        if (!donut) return;

        var hovered = _hover >= 0 && _hover < items.Count ? items[_hover] : null;
        var value = hovered?.SizeText ?? ByteFormatter.Format(items.Where(x => x.Kind != SliceKind.FreeSpace).Sum(x => x.Size));
        var label = hovered?.Label ?? CenterLabel;
        var detail = hovered?.ShareText;
        var maxWidth = inner * 1.6;

        var valueText = Text(value, 22, Foreground, FontWeights.SemiBold, maxWidth);
        var labelText = Text(label, 12, SecondaryForeground, FontWeights.Normal, maxWidth);
        var detailText = detail == null ? null : Text(detail, 12, SecondaryForeground, FontWeights.Normal, maxWidth);
        var height = valueText.Height + labelText.Height + (detailText?.Height ?? 0);
        var y = center.Y - height / 2;
        dc.DrawText(labelText, new Point(center.X - labelText.Width / 2, y));
        y += labelText.Height;
        dc.DrawText(valueText, new Point(center.X - valueText.Width / 2, y));
        y += valueText.Height;
        if (detailText != null) dc.DrawText(detailText, new Point(center.X - detailText.Width / 2, y));
    }

    private static Geometry Segment(Point c, double r, double inner, double startDeg, double sweepDeg)
    {
        if (sweepDeg >= 359.99)
        {
            Geometry full = new EllipseGeometry(c, r, r);
            if (inner > 0) full = new CombinedGeometry(GeometryCombineMode.Exclude, full, new EllipseGeometry(c, inner, inner));
            full.Freeze();
            return full;
        }

        static Point At(Point c, double radius, double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(c.X + Math.Cos(rad) * radius, c.Y + Math.Sin(rad) * radius);
        }

        var large = sweepDeg > 180;
        var end = startDeg + sweepDeg;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (inner > 0)
            {
                ctx.BeginFigure(At(c, inner, startDeg), true, true);
                ctx.LineTo(At(c, r, startDeg), true, true);
                ctx.ArcTo(At(c, r, end), new Size(r, r), 0, large, SweepDirection.Clockwise, true, true);
                ctx.LineTo(At(c, inner, end), true, true);
                ctx.ArcTo(At(c, inner, startDeg), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, true);
            }
            else
            {
                ctx.BeginFigure(c, true, true);
                ctx.LineTo(At(c, r, startDeg), true, true);
                ctx.ArcTo(At(c, r, end), new Size(r, r), 0, large, SweepDirection.Clockwise, true, true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private void RenderBars(DrawingContext dc, IReadOnlyList<ChartItem> items, Rect bounds)
    {
        var rowHeight = Math.Clamp(bounds.Height / items.Count, 22, 34);
        var barHeight = Math.Min(14, rowHeight - 10);
        var labelWidth = Math.Min(170, bounds.Width * 0.34);
        const double valueWidth = 128;
        var barLeft = labelWidth + 12;
        var barMax = Math.Max(10, bounds.Width - barLeft - valueWidth - 8);
        var max = Math.Max(1, items.Max(x => x.Size));
        var top = Math.Max(0, (bounds.Height - rowHeight * items.Count) / 2);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = new Rect(0, top + i * rowHeight, bounds.Width, rowHeight);
            if (row.Bottom > bounds.Height + 0.5) break;
            _hitAreas.Add((new RectangleGeometry(row), i));
            if (i == _hover) dc.DrawRoundedRectangle(HoverBrush, null, row, 4, 4);

            var label = Text(item.Label, 12.5, Foreground, FontWeights.Normal, labelWidth - 8);
            dc.DrawText(label, new Point(labelWidth - label.Width, row.Top + (rowHeight - label.Height) / 2));

            var width = Math.Max(2, barMax * item.Size / max);
            var bar = new Rect(barLeft, row.Top + (rowHeight - barHeight) / 2, width, barHeight);
            var brush = item.Kind == SliceKind.Item ? ChartPalette.Single : item.Brush;
            dc.DrawGeometry(brush, null, RightRoundedBar(bar, Math.Min(4, width / 2)));

            var value = Text($"{item.SizeText}  ·  {item.ShareText}", 12, SecondaryForeground, FontWeights.Normal, valueWidth);
            dc.DrawText(value, new Point(bar.Right + 8, row.Top + (rowHeight - value.Height) / 2));
        }
    }

    private static Geometry RightRoundedBar(Rect r, double radius)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(r.TopLeft, true, true);
            ctx.LineTo(new Point(r.Right - radius, r.Top), true, false);
            ctx.ArcTo(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(r.Right, r.Bottom - radius), true, false);
            ctx.ArcTo(new Point(r.Right - radius, r.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(r.BottomLeft, true, false);
        }

        g.Freeze();
        return g;
    }

    private void RenderTreemap(DrawingContext dc, IReadOnlyList<ChartItem> items, Rect bounds)
    {
        var rects = Squarify(items.Select(x => (double)Math.Max(0, x.Size)).ToList(), bounds);
        var white = Foreground;
        for (var i = 0; i < items.Count; i++)
        {
            var r = rects[i];
            if (r.Width <= 0 || r.Height <= 0) continue;
            r.Inflate(-Gap / 2, -Gap / 2);
            if (r.Width <= 0 || r.Height <= 0) continue;

            _hitAreas.Add((new RectangleGeometry(r), i));
            dc.DrawRoundedRectangle(items[i].Brush, null, r, 4, 4);
            if (i == _hover) dc.DrawRoundedRectangle(null, new Pen(white, 2), r, 4, 4);

            if (r.Width < 56 || r.Height < 36) continue;
            var name = Text(items[i].Label, 12.5, white, FontWeights.SemiBold, r.Width - 16);
            var size = Text($"{items[i].SizeText} · {items[i].ShareText}", 11.5, white, FontWeights.Normal, r.Width - 16);
            dc.PushClip(new RectangleGeometry(r));
            dc.DrawText(name, new Point(r.Left + 8, r.Top + 6));
            if (r.Height > name.Height + size.Height + 10)
                dc.DrawText(size, new Point(r.Left + 8, r.Top + 6 + name.Height));
            dc.Pop();
        }
    }

    /// <summary>Squarified treemap layout (Bruls, Huizing, van Wijk). Values must be sorted descending.</summary>
    internal static List<Rect> Squarify(IReadOnlyList<double> values, Rect bounds)
    {
        var result = new Rect[values.Count];
        var total = values.Sum();
        if (total <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return [.. result];

        var scale = bounds.Width * bounds.Height / total;
        var areas = values.Select(v => v * scale).ToArray();
        var rect = bounds;
        var start = 0;

        while (start < areas.Length)
        {
            var side = Math.Min(rect.Width, rect.Height);
            var end = start + 1;
            var sum = areas[start];
            var worst = Worst(areas, start, end, sum, side);
            while (end < areas.Length)
            {
                var nextSum = sum + areas[end];
                var nextWorst = Worst(areas, start, end + 1, nextSum, side);
                if (nextWorst > worst) break;
                sum = nextSum;
                worst = nextWorst;
                end++;
            }

            if (rect.Width >= rect.Height)
            {
                var width = rect.Height <= 0 ? 0 : sum / rect.Height;
                var y = rect.Top;
                for (var i = start; i < end; i++)
                {
                    var h = width <= 0 ? 0 : areas[i] / width;
                    result[i] = new Rect(rect.Left, y, width, h);
                    y += h;
                }

                rect = new Rect(rect.Left + width, rect.Top, Math.Max(0, rect.Width - width), rect.Height);
            }
            else
            {
                var height = rect.Width <= 0 ? 0 : sum / rect.Width;
                var x = rect.Left;
                for (var i = start; i < end; i++)
                {
                    var w = height <= 0 ? 0 : areas[i] / height;
                    result[i] = new Rect(x, rect.Top, w, height);
                    x += w;
                }

                rect = new Rect(rect.Left, rect.Top + height, rect.Width, Math.Max(0, rect.Height - height));
            }

            start = end;
        }

        return [.. result];
    }

    private static double Worst(double[] areas, int start, int end, double sum, double side)
    {
        if (sum <= 0 || side <= 0) return double.MaxValue;
        double max = 0, min = double.MaxValue;
        for (var i = start; i < end; i++)
        {
            max = Math.Max(max, areas[i]);
            min = Math.Min(min, areas[i]);
        }

        if (min <= 0) return double.MaxValue;
        var s2 = sum * sum;
        var w2 = side * side;
        return Math.Max(w2 * max / s2, s2 / (w2 * min));
    }

    private void RenderTooltip(DrawingContext dc, ChartItem item, Rect bounds)
    {
        const double pad = 10;
        var title = Text(item.Label, 12.5, Foreground, FontWeights.SemiBold, 280);
        var value = Text($"{item.SizeText}  ·  {item.ShareText}", 12, SecondaryForeground, FontWeights.Normal, 280);
        var hint = item.Node is { IsDirectory: true }
            ? Text(Loc.T("chart.click_to_open"), 11, SecondaryForeground, FontWeights.Normal, 280)
            : null;

        var width = Math.Max(title.Width, Math.Max(value.Width, hint?.Width ?? 0)) + pad * 2 + 14;
        var height = title.Height + value.Height + (hint == null ? 0 : hint.Height + 4) + pad * 2;
        var x = _mouse.X + 14;
        var y = _mouse.Y + 14;
        if (x + width > bounds.Width) x = Math.Max(0, _mouse.X - width - 10);
        if (y + height > bounds.Height) y = Math.Max(0, _mouse.Y - height - 10);

        var box = new Rect(x, y, width, height);
        dc.DrawRoundedRectangle(TooltipBackground, new Pen(TooltipBorderBrush, 1), box, 6, 6);
        dc.DrawRoundedRectangle(item.Brush, null, new Rect(x + pad, y + pad + 3, 8, 8), 2, 2);
        dc.DrawText(title, new Point(x + pad + 14, y + pad));
        dc.DrawText(value, new Point(x + pad + 14, y + pad + title.Height));
        if (hint != null) dc.DrawText(hint, new Point(x + pad + 14, y + pad + title.Height + value.Height + 4));
    }

    private FormattedText Text(string text, double size, Brush brush, FontWeight weight, double maxWidth)
    {
        var family = (FontFamily)GetValue(TextElement.FontFamilyProperty);
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        if (maxWidth > 0) formatted.MaxTextWidth = maxWidth;
        return formatted;
    }

    // ───────────── Interaction ─────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.GetPosition(this);
        var hit = -1;
        foreach (var (shape, index) in _hitAreas)
        {
            if (shape.FillContains(_mouse))
            {
                hit = index;
                break;
            }
        }

        _hover = hit;
        var items = Items;
        Cursor = hit >= 0 && items != null && hit < items.Count && items[hit].CanNavigate ? Cursors.Hand : null;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Cursor = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var items = Items;
        if (_hover < 0 || items == null || _hover >= items.Count) return;
        var item = items[_hover];
        if (item.CanNavigate && ItemCommand?.CanExecute(item) == true)
        {
            _hover = -1;
            ItemCommand.Execute(item);
        }
    }
}
