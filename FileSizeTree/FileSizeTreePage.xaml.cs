using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Unity;

namespace FileSizeTree
{
    /// <summary>
    /// Interaction logic for FileTreePage.xaml
    /// </summary>
    public partial class FileSizeTreePage : UserControl
    {
        public FileSizeTreePage()
        {
            InitializeComponent();
        }

        [Dependency]
        public FileSizeTreePageViewModel ViewModel
        {
            get => DataContext as FileSizeTreePageViewModel;
            set => DataContext = value;
        }
    }
}
