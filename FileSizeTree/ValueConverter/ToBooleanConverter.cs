using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace FileSizeTree.ValueConverter
{
    public class ToBooleanConverter : IValueConverter
    {
        public bool IsInverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool rval = false;
            if (value is bool boolValue)
                rval = boolValue;
            else if (value != null)
                rval = true;

            if (IsInverted)
                rval = !rval;

            return rval;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
