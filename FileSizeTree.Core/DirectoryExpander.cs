using System;
using System.IO;
using System.Threading.Tasks;

namespace FileSizeTree.Core
{
    public class DirectoryExpander : IElementExpander
    {
        public void Expand(Element element)
        {
            if (element.Type != ElementType.Directory)
                return;

            ExpandElement(element);
        }

        private void ExpandElement(Element element)
        {
            var directoryInfo = new DirectoryInfo(element.Path);
            var elementFinalSize = 0L;
            var elementFileCount = 0;

            FileInfo[] fileInfos = null;
            try
            {
                fileInfos = directoryInfo.GetFiles();
            }
            catch (UnauthorizedAccessException)
            {
                // ignore unauthorized access folders.
            }

            if (fileInfos != null)
            {
                foreach (var fileInfo in fileInfos)
                {
                    var length = fileInfo.Length;
                    element.Children.Add(new Element(this, ElementType.File, fileInfo.FullName, element, length));
                    System.Threading.Interlocked.Add(ref elementFinalSize, fileInfo.Length);
                    System.Threading.Interlocked.Increment(ref elementFileCount);
                }
            }

            DirectoryInfo[] dirs = null;
            try
            {
                dirs = directoryInfo.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                // ignore unauthorized access folders.
            }
            if (dirs != null)
            {
                var lockObject = new object();
                Parallel.ForEach(dirs, (subDirectory) =>
                {
                    var subElement = new Element(this, ElementType.Directory, subDirectory.FullName, element);
                    subElement.Expand();
                    lock (lockObject)
                        element.Children.Add(subElement);
                    System.Threading.Interlocked.Add(ref elementFinalSize, subElement.Size);
                    System.Threading.Interlocked.Add(ref elementFileCount, subElement.FileCount);
                });
            }

            element.Size = elementFinalSize;
            element.FileCount = elementFileCount;
        }
    }
}
