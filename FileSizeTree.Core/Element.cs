using System;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace FileSizeTree.Core
{
    [DebuggerDisplay("{Type}, {Size}, {Path}")]
    public class Element : INotifyPropertyChanged, IElementExpand
    {
        public long Size { get; set; }
        public string Path { get; private set; }
        public string Name { get; private set; }
        public ElementType Type { get; } = ElementType.Directory;
        public Element Parent { get; set; }
        public ObservableCollection<Element> Children { get; }
        public int FileCount { get; set; }
        public int Mag { get; private set; }

        private readonly IElementExpander _expander;

        public Element(IElementExpander expander, ElementType type, string path, Element parent, long size = 0)
        {
            Type = type;
            Path = path ?? throw new ArgumentNullException(nameof(path));
            _expander = expander ?? throw new ArgumentNullException(nameof(expander));
            Parent = parent;
            Children = new ObservableCollection<Element>();
            Size = size;
            Name = System.IO.Path.GetFileName(Path);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async void Expand()
        {
            Size = 0;
            Children.Clear();
            _expander.Expand(this);
            if (Children.Any())
                Mag = Children
                    .Select(elm => (int)Math.Log(elm.Size, 1024))
                    .Max();

            RaisePropertyChanged(nameof(Size));
            RaisePropertyChanged(nameof(Children));
            RaisePropertyChanged(nameof(Mag));
        }
    }
}
