using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FileSizeTree.Core;
using FileSizeTree.FileOperation;
using Prism.Ioc;
using Prism.Unity;
using Unity;

namespace FileSizeTree
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        public App()
        {
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<AppShellWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<IElementExpander, DirectoryExpander>();
            containerRegistry.Register<IFileOperation, FileOperation.FileOperation>();

            // Pages
            containerRegistry.RegisterForNavigation<FileSizeTreePage>();

            containerRegistry.RegisterSingleton<IsBusyService.IIsBusyService, IsBusyService.IsBusyService>();
        }
    }
}
