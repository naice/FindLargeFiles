using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace FileSizeTree.Behaviors
{
    public class BindableSelectedItemBehavior : Behavior<TreeView>
    {
        #region SelectedItem Property

        public object SelectedItem
        {
            get { return (object)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object), typeof(BindableSelectedItemBehavior), new UIPropertyMetadata(null, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as BindableSelectedItemBehavior).OnSelectedItemChange(e.NewValue, e.OldValue);
        }

        #endregion

        private void OnSelectedItemChange(object newItem, object oldItem)
        {
            if (newItem != null && AssociatedObject.ItemContainerGenerator.ContainerFromItem(newItem) is TreeViewItem itemToSelect)
                itemToSelect.IsSelected = true;

            if (oldItem != null && AssociatedObject.ItemContainerGenerator.ContainerFromItem(oldItem) is TreeViewItem itemToDeselect)
                itemToDeselect.IsSelected = false;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject != null)
            {
                AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
            }
        }

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.SelectedItem = e.NewValue;
        }
    }
}
