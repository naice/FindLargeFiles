using FileSizeTree.Controls;
using FileSizeTree.Core;
using FileSizeTree.IsBusyService;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity;

namespace FileSizeTree
{
    public class AppShellWindowViewModel : BindableBase, IShellWindowLoaded
    {
        private readonly IRegionManager _regionManager;

        public AppShellWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        }

        public void OnShellWindowLoaded()
        {
            _regionManager.RequestNavigate(Navigation.NavigationConstants.REGION_NAME_APPSHELL, nameof(FileSizeTreePage));
        }
    }
}
