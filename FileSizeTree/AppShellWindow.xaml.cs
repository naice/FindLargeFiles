using FileSizeTree.Controls;
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
using System.Windows.Shapes;
using Unity;

namespace FileSizeTree
{
    /// <summary>
    /// Interaction logic for AppShellWindow.xaml
    /// </summary>
    public partial class AppShellWindow : ShellWindow
    {
        public AppShellWindow()
        {
            InitializeComponent();
        }

        [Dependency]
        public AppShellWindowViewModel ViewModel
        {
            get => DataContext as AppShellWindowViewModel;
            set => DataContext = value;
        }
    }
}
