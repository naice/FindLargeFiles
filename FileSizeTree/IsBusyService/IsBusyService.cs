using FileSizeTree.Controls;
using System;
using System.Linq;
using System.Windows;

namespace FileSizeTree.IsBusyService
{
    public class IsBusyService : IIsBusyService
    {
        public IDisposable GetIsBusyContext()
        {
            return GetIsBusyContext(TimeSpan.Zero);
        }

        public IDisposable GetIsBusyContext(TimeSpan delay)
        {
            return new IsBusyContext(SetAllWinowsIsBusyTrue, SetAllWinowsIsBusyFalse, delay);
        }

        private void SetAllWinowsIsBusyFalse()
        {
            SetAllWindowsIsBusy(isBusy: false);
        }
        private void SetAllWinowsIsBusyTrue()
        {
            SetAllWindowsIsBusy(isBusy: true);
        }

        private void SetAllWindowsIsBusy(bool isBusy)
        {
            foreach (var shellWindow in Application.Current.Windows.OfType<ShellWindow>())
            {
                shellWindow.BusyIndicator.IsBusy = isBusy;
            }
        }
    }
}
