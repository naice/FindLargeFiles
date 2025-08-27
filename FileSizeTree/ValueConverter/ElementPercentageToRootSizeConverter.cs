using FileSizeTree.Core;
using System;
using System.Globalization;
using System.Windows.Data;

namespace FileSizeTree.ValueConverter
{
    public class ElementPercentageToRootSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Element element))
                return 100;

            var root = GetParent(element);
            double factor = (double)element.Size / (double)root.Size;
            return factor;
        }

        private Element GetParent(Element element)
        {
            if (element.Parent != null)
                element = element.Parent;

            return element;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
