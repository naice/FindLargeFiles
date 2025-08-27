using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileSizeTree.FileOperation
{
    public interface IFileOperation
    {
        bool Delete(string path);
    }

    public class FileOperation : IFileOperation
    {
        public bool Delete(string path)
        {
            var fileOperation = new InteropSHFileOperation();
            fileOperation.pFrom = path;
            fileOperation.pTo = path;
            fileOperation.fFlags.FOF_ALLOWUNDO = true;
            fileOperation.wFunc = InteropSHFileOperation.FO_Func.FO_DELETE;

            return fileOperation.Execute();
        }
    }
}
