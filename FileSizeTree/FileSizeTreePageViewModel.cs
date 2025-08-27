using FileSizeTree.Core;
using FileSizeTree.FileOperation;
using FileSizeTree.IsBusyService;
using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FileSizeTree
{
    public class FileSizeTreePageViewModel : BindableBase
    {
        [DllImport("shell32.dll", EntryPoint = "ShellExecute")]
        public static extern long ShellExecute(int hwnd, string cmd, string file, string param1, string param2, int swmode);

        private readonly IFileOperation _fileOperation;
        private readonly IRegionManager _regionManager;
        private readonly IElementExpander _elementExpander;
        private readonly IIsBusyService _isBusyService;

        public EasyCommand OpenDirectory { get; }
        public EasyCommand RefreshDirectory { get; }

        public EasyCommand<Element> Open { get; }
        public EasyCommand<Element> ScopeToThis { get; }
        public EasyCommand<Element> Delete { get; }
        public EasyCommand<Element> Properties { get; }
        public EasyCommand CloseProperties { get; }


        private Element _Element;
        public Element Element
        {
            get => _Element;
            set => SetProperty(ref _Element, value, () => RefreshDirectory.IsExecutable = _Element != null);
        }

        private Element _TreeViewSelected;
        public Element TreeViewSelected
        {
            get => _TreeViewSelected;
            set => SetProperty(ref _TreeViewSelected, value);
        }

        private Element _PropertiesElement;
        public Element PropertiesElement
        {
            get => _PropertiesElement;
            set => SetProperty(ref _PropertiesElement, value, ()=> TreeViewSelected = _PropertiesElement);
        }



        public FileSizeTreePageViewModel(
            IElementExpander elementExpander, 
            IIsBusyService isBusyService, 
            IFileOperation fileOperation,
            IRegionManager regionManager)
        {
            _fileOperation = fileOperation ?? throw new ArgumentNullException(nameof(fileOperation));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _elementExpander = elementExpander ?? throw new ArgumentNullException(nameof(elementExpander));
            _isBusyService = isBusyService ?? throw new ArgumentNullException(nameof(isBusyService));
            OpenDirectory = new EasyCommand(Execute_OpenDirectory);
            RefreshDirectory = new EasyCommand(Execute_RefreshDirectory);
            RefreshDirectory.IsExecutable = false;
            Open = new EasyCommand<Element>(Execute_Open);
            ScopeToThis = new EasyCommand<Element>(Execute_ScopeToThis);
            Delete = new EasyCommand<Element>(Execute_Delete);
            Properties = new EasyCommand<Element>(Execute_Properties);
            CloseProperties = new EasyCommand(Execute_CloseProperties);
        }

        private void Execute_CloseProperties()
        {
            PropertiesElement = null;
        }

        private void Execute_Properties(Element element)
        {
            PropertiesElement = element;
        }

        private async void ExpandDirectoryAndSetElement(string dir)
        {
            using (_isBusyService.GetIsBusyContext(TimeSpan.FromMilliseconds(200)))
            {
                var element = new Element(_elementExpander, ElementType.Directory, dir, null);

                await Task.Run(() => element.Expand()).ConfigureAwait(true);

                Element = element;
            }
        }

        private async void Execute_Delete(Element element)
        {
            using (Delete.ExecutingContext)
            {
                await Task.Run(() =>
                {
                    if (!Directory.Exists(element.Path) && !File.Exists(element.Path))
                        return;

                    if (_fileOperation.Delete(element.Path))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            element.Parent.Children.Remove(element);
                        });
                    }
                }).ConfigureAwait(true);
            }
        }

        private void Execute_ScopeToThis(Element element)
        {
            if (Element == null)
                return;

            ExpandDirectoryAndSetElement(element.Path);
        }

        private void Execute_Open(Element element)
        {
            if (element == null)
                return;
            if (element.Type == ElementType.Directory)
                ShellExecute(0, "open", element.Path, "", "", 5);
            else if (element.Type == ElementType.File)
                ShellExecute(0, "explorer", element.Path, "", "", 5);
        }

        private void Execute_RefreshDirectory()
        {
            using (RefreshDirectory.ExecutingContext)
            {
                if (Element == null)
                    return;

                if (!Directory.Exists(Element.Path))
                {
                    Element = null;
                    return;
                }

                ExpandDirectoryAndSetElement(Element.Path);
            }
        }

        private void Execute_OpenDirectory()
        {
            using (OpenDirectory.ExecutingContext)
            {
                var dir = Extensions.OpenDirectory();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    return;

                ExpandDirectoryAndSetElement(dir);
            }
        }


    }
}
