using Prism.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace FileSizeTree
{
    public class EnsureExecute : IDisposable
    {
        private readonly Action _action;
        public EnsureExecute(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action?.Invoke();
        }
    }

    public class EasyCommand<T> : DelegateCommand<T>, INotifyPropertyChanged
    {
        public IDisposable ExecutingContext
        {
            get
            {
                IsExecutable = false;
                IsExecuting = true;
                return new EnsureExecute(() => {
                    IsExecutable = true;
                    IsExecuting = false;
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private bool _IsExecuting;
        public bool IsExecuting
        {
            get { return _IsExecuting; }
            set
            {
                if (value != _IsExecuting)
                {
                    _IsExecuting = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsNotExecuting));
                }
            }
        }

        public bool IsNotExecuting => !_IsExecuting;


        private bool _IsExecutable = true;

        public bool IsExecutable
        {
            get => _IsExecutable;
            set
            {
                if (value != _IsExecutable)
                {
                    _IsExecutable = value;
                    RaiseCanExecuteChanged();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsNotExecuteable));
                }
            }
        }

        public bool IsNotExecuteable => !IsExecutable;

        public EasyCommand(Action<T> executeMethod) : base(executeMethod)
        {

        }

        protected override bool CanExecute(object parameter)
        {
            return IsExecutable;
        }
    }

    public class EasyCommand : DelegateCommand, INotifyPropertyChanged
    {
        public IDisposable ExecutingContext 
        { 
            get {
                IsExecutable = false;
                IsExecuting = true;
                return new EnsureExecute(() => {
                    IsExecutable = true;
                    IsExecuting = false;
                });
             } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private bool _IsExecuting;
        public bool IsExecuting
        {
            get { return _IsExecuting; }
            set
            {
                if (value != _IsExecuting)
                {
                    _IsExecuting = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsNotExecuting));
                }
            }
        }

        public bool IsNotExecuting => !_IsExecuting;


        private bool _IsExecutable = true;

        public bool IsExecutable
        {
            get => _IsExecutable;
            set
            {
                if (value != _IsExecutable)
                {
                    _IsExecutable = value;
                    RaiseCanExecuteChanged();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsNotExecuteable));
                }
            }
        }

        public bool IsNotExecuteable => !IsExecutable;

        public EasyCommand(Action executeMethod) : base(executeMethod)
        {
            
        }

        protected override bool CanExecute(object parameter)
        {
            return IsExecutable;
        }
    }
}
