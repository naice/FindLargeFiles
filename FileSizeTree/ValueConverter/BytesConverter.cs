using FileSizeTree.Core;
using System;
using System.Globalization;
using System.Windows.Data;

namespace FileSizeTree.ValueConverter
{
    public class BytesConverter : IValueConverter
    {
        public bool IsAutoDetecting { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Element element))
                return null;

            return SizeSuffix(element.Size, 2, IsAutoDetecting ? -1 : element.Parent?.Mag ?? element.Mag);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        static readonly string[] SizeSuffixes =
            { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(long value, int decimalPlaces = 1, int magg = -1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} {1}", 0, SizeSuffixes[magg < 0 ? 0 : magg]); }
            int mag;

            if (magg < 0)
                // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
                mag = (int)Math.Log(value, 1024);
            else
                mag = magg;

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));


            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}
