using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace FileSizeTree.ValueConverter
{
    public class ToVisibilityConverter : IValueConverter
    {
        public bool IsInverted { get; set; } = false;
        public bool IsCollapsed { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = false;

            if (value is bool boolValue)
                visible = boolValue;
            else if (value != null)
                visible = true;

            if (IsInverted)
                visible = !visible;

            return visible
                ? Visibility.Visible
                : IsCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
