using System;
using System.Threading.Tasks;
using System.Windows;

namespace FileSizeTree.IsBusyService
{
    public class IsBusyContext : IDisposable
    {
        private readonly Action _executeOnDispose;
        private readonly Action _executeAfterDelayOrInterrupt;
        private readonly TimeSpan _delay;

        private bool _isInterrupted = false;

        public IsBusyContext(Action executeAfterDelayOrInterrupt, Action executeOnDispose, TimeSpan delay)
        {
            _executeOnDispose = executeOnDispose ?? throw new ArgumentNullException(nameof(executeOnDispose));
            _executeAfterDelayOrInterrupt = executeAfterDelayOrInterrupt ?? throw new ArgumentNullException(nameof(executeAfterDelayOrInterrupt));
            _delay = delay;

            if (delay == TimeSpan.Zero)
                _executeAfterDelayOrInterrupt.Invoke();
            else
                DelayedAndInterruptedExecution();
        }

        private async void DelayedAndInterruptedExecution()
        {
            await Task.Delay(_delay).ConfigureAwait(true);

            if (_isInterrupted)
                return;

            _executeAfterDelayOrInterrupt.Invoke();
        }

        public void Dispose()
        {
            _isInterrupted = true;
            _executeOnDispose.Invoke();
        }
    }
}
