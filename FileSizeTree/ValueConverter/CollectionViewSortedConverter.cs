using FileSizeTree.Core;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace FileSizeTree.ValueConverter
{
    public class CollectionViewSortedConverter : IValueConverter
    {
        public ListSortDirection SortDirection { get; set; } = ListSortDirection.Descending;
        public string PropertyName { get; set; }
        public long MinimumFileSize { get; set; } = 0;

        public bool IsFileFiltered { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is IList<Element> collection))
                return null;

            if (IsFileFiltered)
                collection = collection.Where(elm => elm.Type == ElementType.Directory && elm.Size > 0).ToList();

            if (MinimumFileSize > 0 && !IsFileFiltered)
                collection = collection.Where(elm => elm.Type == ElementType.Directory || elm.Size > MinimumFileSize).ToList();

            ListCollectionView view = new ListCollectionView(collection as IList);
            SortDescription sort = new SortDescription(PropertyName, SortDirection);
            view.SortDescriptions.Add(sort);
            return view;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
