using System;
using System.Collections.Generic;
using System.Text;

namespace FileSizeTree.IsBusyService
{
    public interface IIsBusyService
    {
        IDisposable GetIsBusyContext();
        IDisposable GetIsBusyContext(TimeSpan delay);
    }
}
