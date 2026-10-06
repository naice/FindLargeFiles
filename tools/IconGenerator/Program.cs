using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGenerator;

/// <summary>
/// Draws the FindLargeFiles icon candidates on a 256×256 canvas and writes, per candidate,
/// a multi-size .ico, a 256 px .png and a combined preview sheet.
/// Usage: dotnet run --project tools/IconGenerator -- [outputDir]
/// </summary>
internal static class Program
{
    private static readonly int[] IcoSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    private static readonly (string Id, string Name, string Idea, Action<DrawingContext> Draw)[] Icons =
    [
        ("1-ring-lupe", "Ring & Lupe", "Ringdiagramm als Linse einer Lupe", RingMagnifier),
        ("2-treemap", "Treemap", "Speicherblöcke wie in der Treemap-Ansicht", Treemap),
        ("3-balken", "Sortierte Balken", "Größte zuerst – wie die Baumliste", SortedBars),
        ("4-ordner-ring", "Ordner & Ring", "Ordner mit Diagramm-Badge", FolderRing),
        ("5-lupe-balken", "Lupe & Balken", "Lupe findet den größten Balken", MagnifierBars),
    ];

    // Palette shared with the app (chart slots, accent, surfaces).
    private static readonly Color Blue = C("#3987E5"), Orange = C("#EB6834"), Green = C("#1BAF7A"),
        Yellow = C("#EDA100"), Magenta = C("#E87BA4"), Violet = C("#9085E9"), Ink = C("#E7E9F0"),
        Amber = C("#FFC857"), Folder = C("#E8B04A"), FolderBack = C("#C48A2C");

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "assets/icons");
        Directory.CreateDirectory(output);

        foreach (var icon in Icons)
        {
            var frames = IcoSizes.Select(size => Png(Render(icon.Draw, size))).ToList();
            WriteIco(Path.Combine(output, $"option-{icon.Id}.ico"), IcoSizes, frames);
            File.WriteAllBytes(Path.Combine(output, $"option-{icon.Id}.png"), frames[^1]);
            Console.WriteLine($"option-{icon.Id}.ico");
        }

        File.WriteAllBytes(Path.Combine(output, "preview.png"), Png(PreviewSheet()));
        Console.WriteLine("preview.png");
    }

    // ───────────── Candidates ─────────────

    /// <summary>A donut chart that doubles as the lens of a magnifying glass.</summary>
    private static void RingMagnifier(DrawingContext dc)
    {
        Tile(dc, C("#323855"), C("#1B1E2B"));
        var center = new Point(110, 110);
        Donut(dc, center, 70, 40, [(0.50, Blue), (0.22, Orange), (0.15, Green), (0.13, Yellow)], 4);
        dc.DrawEllipse(null, Stroke(Ink, 11), center, 86, 86);
        var a = Math.PI / 4;
        dc.DrawLine(Stroke(Ink, 26), new Point(center.X + Math.Cos(a) * 92, center.Y + Math.Sin(a) * 92), new Point(206, 206));
    }

    /// <summary>Squarified blocks in the chart palette, the largest one dominant.</summary>
    private static void Treemap(DrawingContext dc)
    {
        Tile(dc, C("#2A2F45"), C("#181B26"));
        Block(dc, Blue, 40, 40, 100, 176, 14);
        Block(dc, Orange, 148, 40, 68, 84, 12);
        Block(dc, Green, 148, 132, 68, 44, 10);
        Block(dc, Yellow, 148, 184, 32, 32, 8);
        Block(dc, Magenta, 188, 184, 28, 32, 8);
    }

    /// <summary>Bars sorted by size; the largest is highlighted as "found".</summary>
    private static void SortedBars(DrawingContext dc)
    {
        Tile(dc, C("#8285F7"), C("#4F46E5"));
        double[] widths = [148, 112, 80, 48];
        for (var i = 0; i < widths.Length; i++)
        {
            var brush = i == 0 ? new SolidColorBrush(Amber) : new SolidColorBrush(Color.FromArgb(i == 1 ? (byte)235 : i == 2 ? (byte)190 : (byte)150, 255, 255, 255));
            dc.DrawRoundedRectangle(brush, null, new Rect(54, 58 + i * 38, widths[i], 26), 13, 13);
        }
    }

    /// <summary>A classic folder with a donut chart badge.</summary>
    private static void FolderRing(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(new SolidColorBrush(FolderBack), null, new Rect(18, 34, 96, 44), 14, 14);
        dc.DrawRoundedRectangle(new SolidColorBrush(FolderBack), null, new Rect(18, 52, 214, 150), 18, 18);
        var front = new LinearGradientBrush(C("#F5C766"), Folder, 90);
        dc.DrawRoundedRectangle(front, null, new Rect(18, 80, 214, 124), 18, 18);

        var center = new Point(182, 182);
        dc.DrawEllipse(new SolidColorBrush(C("#1B1E2B")), null, center, 66, 66);
        Donut(dc, center, 54, 30, [(0.46, Blue), (0.24, Orange), (0.17, Green), (0.13, Violet)], 5);
    }

    /// <summary>A magnifying glass whose lens shows bars; the tallest one stands out.</summary>
    private static void MagnifierBars(DrawingContext dc)
    {
        Tile(dc, C("#8285F7"), C("#4F46E5"));
        var center = new Point(108, 108);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 20, 18, 80)), null, center, 62, 62);
        dc.PushClip(new EllipseGeometry(center, 56, 56));
        TopRoundedBar(dc, Green, 72, 150, 22, 40);
        TopRoundedBar(dc, Amber, 99, 150, 22, 84);
        TopRoundedBar(dc, Blue.WithLightness(1.15), 126, 150, 22, 58);
        dc.Pop();
        dc.DrawEllipse(null, Stroke(Colors.White, 16), center, 62, 62);
        dc.DrawLine(Stroke(Colors.White, 28), new Point(156, 156), new Point(206, 206));
    }

    // ───────────── Drawing helpers ─────────────

    private static void Tile(DrawingContext dc, Color top, Color bottom)
    {
        var rect = new Rect(8, 8, 240, 240);
        dc.DrawRoundedRectangle(new LinearGradientBrush(top, bottom, 90), null, rect, 56, 56);
        var sheen = new LinearGradientBrush(Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
        dc.DrawRoundedRectangle(sheen, new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 2), rect, 56, 56);
    }

    private static void Block(DrawingContext dc, Color color, double x, double y, double w, double h, double r) =>
        dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(x, y, w, h), r, r);

    private static void TopRoundedBar(DrawingContext dc, Color color, double x, double bottom, double width, double height)
    {
        const double r = 6;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(x, bottom + 20), true, true);
            ctx.LineTo(new Point(x, bottom - height + r), false, false);
            ctx.ArcTo(new Point(x + r, bottom - height), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            ctx.LineTo(new Point(x + width - r, bottom - height), false, false);
            ctx.ArcTo(new Point(x + width, bottom - height + r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            ctx.LineTo(new Point(x + width, bottom + 20), false, false);
        }

        dc.DrawGeometry(new SolidColorBrush(color), null, g);
    }

    private static void Donut(DrawingContext dc, Point c, double rOut, double rIn, (double Share, Color Color)[] slices, double gapDeg)
    {
        var start = -90.0;
        foreach (var (share, color) in slices)
        {
            var sweep = share * 360;
            var s = start + gapDeg / 2;
            var e = start + sweep - gapDeg / 2;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                var large = e - s > 180;
                ctx.BeginFigure(At(c, rIn, s), true, true);
                ctx.LineTo(At(c, rOut, s), false, false);
                ctx.ArcTo(At(c, rOut, e), new Size(rOut, rOut), 0, large, SweepDirection.Clockwise, false, false);
                ctx.LineTo(At(c, rIn, e), false, false);
                ctx.ArcTo(At(c, rIn, s), new Size(rIn, rIn), 0, large, SweepDirection.Counterclockwise, false, false);
            }

            dc.DrawGeometry(new SolidColorBrush(color), null, g);
            start += sweep;
        }
    }

    private static Point At(Point c, double r, double deg)
    {
        var rad = deg * Math.PI / 180;
        return new Point(c.X + Math.Cos(rad) * r, c.Y + Math.Sin(rad) * r);
    }

    private static Pen Stroke(Color color, double width) =>
        new(new SolidColorBrush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color WithLightness(this Color c, double f) =>
        Color.FromRgb((byte)Math.Min(255, c.R * f), (byte)Math.Min(255, c.G * f), (byte)Math.Min(255, c.B * f));

    // ───────────── Output ─────────────

    private static BitmapSource Render(Action<DrawingContext> draw, int size)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetEdgeMode(visual, EdgeMode.Unspecified);
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
            draw(dc);
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] Png(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>ICO container with PNG-compressed frames (supported since Windows Vista).</summary>
    private static void WriteIco(string path, int[] sizes, IReadOnlyList<byte[]> frames)
    {
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)frames.Count);
        var offset = 6 + 16 * frames.Count;
        for (var i = 0; i < frames.Count; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(frames[i].Length);
            w.Write(offset);
            offset += frames[i].Length;
        }

        foreach (var frame in frames) w.Write(frame);
    }

    /// <summary>All candidates side by side: large, plus small sizes on dark and light backgrounds.</summary>
    private static BitmapSource PreviewSheet()
    {
        const int col = 300, width = col * 5 + 40, height = 640;
        var visual = new DrawingVisual();
        var face = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var faceRegular = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        FormattedText Text(string s, double size, Color color, Typeface t) =>
            new(s, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, t, size, new SolidColorBrush(color), 1.0);

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(C("#181B24")), null, new Rect(0, 0, width, height));
            for (var i = 0; i < Icons.Length; i++)
            {
                var (id, name, idea, draw) = Icons[i];
                var x = 20 + i * col;
                dc.DrawRoundedRectangle(new SolidColorBrush(C("#212534")), new Pen(new SolidColorBrush(C("#2E3445")), 1), new Rect(x + 6, 20, col - 12, height - 40), 14, 14);

                dc.DrawImage(Render(draw, 200), new Rect(x + 50, 44, 200, 200));
                var title = Text($"{i + 1}  {name}", 18, C("#E7E9F0"), face);
                dc.DrawText(title, new Point(x + (col - title.Width) / 2, 262));
                var sub = Text(idea, 12.5, C("#A4ABBC"), faceRegular);
                dc.DrawText(sub, new Point(x + (col - sub.Width) / 2, 290));

                // Small sizes, as in taskbar, title bar and Explorer.
                int[] small = [64, 48, 32, 24, 16];
                foreach (var (bg, y, label) in new[] { (C("#202020"), 340.0, "Dunkel"), (C("#F3F3F3"), 470.0, "Hell") })
                {
                    var strip = new Rect(x + 22, y, col - 44, 110);
                    dc.DrawRoundedRectangle(new SolidColorBrush(bg), null, strip, 10, 10);
                    var cx = strip.Left + 14;
                    foreach (var s in small)
                    {
                        // Drawn at native pixel size so the preview shows the real small-size rendering.
                        dc.DrawImage(Render(draw, s), new Rect(cx, y + 20 + (64 - s), s, s));
                        cx += s + 12;
                    }

                    var l = Text(label, 11, bg.R > 128 ? C("#66655F") : C("#8A90A0"), faceRegular);
                    dc.DrawText(l, new Point(strip.Left + 14, y + 90));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
}
